using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.Payments;
using Homeji.Application.Mappers.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class PaymentService : IPaymentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly UserContext _userContext;
    private readonly IPaymentTransactionRepository _payments;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly MomoOptions _momoOptions;
    private readonly PayOsOptions _payOsOptions;
    private readonly PremiumSubscriptionOptions _premiumOptions;
    private readonly TimeProvider _timeProvider;

    public PaymentService(
        HttpClient httpClient,
        UserContext userContext,
        IPaymentTransactionRepository payments,
        IUserSubscriptionRepository subscriptions,
        IOptions<MomoOptions> momoOptions,
        IOptions<PayOsOptions> payOsOptions,
        IOptions<PremiumSubscriptionOptions> premiumOptions,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _userContext = userContext;
        _payments = payments;
        _subscriptions = subscriptions;
        _momoOptions = momoOptions.Value;
        _payOsOptions = payOsOptions.Value;
        _premiumOptions = premiumOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<PaymentDto> GetPaymentByIdAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var payment = await _payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), paymentId);

        UserContext.EnsureOwner(userId, payment.UserId);
        return PaymentMapper.ToDto(payment);
    }

    public async Task<PaymentDto> GetPaymentByOrderCodeAsync(
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            throw Validation("orderCode", "Order code is required.");
        }

        var userId = _userContext.GetRequiredUserId();
        var payment = await _payments.GetByOrderCodeAsync(orderCode, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), orderCode);

        UserContext.EnsureOwner(userId, payment.UserId);
        return PaymentMapper.ToDto(payment);
    }

    public async Task<MomoPaymentResponseDto> CreateMomoPaymentAsync(
        CreateMomoPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        return await CreateMomoPaymentInternalAsync(
            request.Amount,
            request.Description,
            PaymentPurpose.General,
            null,
            cancellationToken);
    }

    public async Task<MomoPaymentResponseDto> CreatePremiumMomoPaymentAsync(
        CancellationToken cancellationToken = default)
    {
        EnsurePremiumConfigured();

        return await CreateMomoPaymentInternalAsync(
            _premiumOptions.Price,
            BuildPremiumDescription(),
            PaymentPurpose.PremiumSubscription,
            _premiumOptions.Code,
            cancellationToken);
    }

    private async Task<MomoPaymentResponseDto> CreateMomoPaymentInternalAsync(
        decimal requestedAmount,
        string? requestedDescription,
        PaymentPurpose purpose,
        string? packageCode,
        CancellationToken cancellationToken)
    {
        EnsureMomoConfigured();
        var userId = _userContext.GetRequiredUserId();
        var amount = ToWholeVnd(requestedAmount);
        var description = NormalizeDescription(requestedDescription);
        var now = _timeProvider.GetUtcNow();
        var orderCode = $"MOMO{now.ToUnixTimeMilliseconds()}";
        var requestId = $"{orderCode}{RandomNumberGenerator.GetInt32(1000, 9999)}";
        const string extraData = "";

        var rawSignature = string.Join(
            "&",
            $"accessKey={_momoOptions.AccessKey}",
            $"amount={amount}",
            $"extraData={extraData}",
            $"ipnUrl={_momoOptions.IpnUrl}",
            $"orderId={orderCode}",
            $"orderInfo={description}",
            $"partnerCode={_momoOptions.PartnerCode}",
            $"redirectUrl={_momoOptions.RedirectUrl}",
            $"requestId={requestId}",
            $"requestType={_momoOptions.RequestType}");

        var payload = new
        {
            partnerCode = _momoOptions.PartnerCode,
            accessKey = _momoOptions.AccessKey,
            requestId,
            amount,
            orderId = orderCode,
            orderInfo = description,
            redirectUrl = _momoOptions.RedirectUrl,
            ipnUrl = _momoOptions.IpnUrl,
            extraData,
            requestType = _momoOptions.RequestType,
            signature = Sign(rawSignature, _momoOptions.SecretKey),
            lang = _momoOptions.Lang,
        };

        using var response = await _httpClient.PostAsJsonAsync(_momoOptions.Endpoint, payload, JsonOptions, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Validation("momo", responseText);
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        var payment = new PaymentTransaction(userId, PaymentMethod.Momo, amount, orderCode, description, now, purpose, packageCode);
        payment.AttachMomoPayment(
            requestId,
            GetString(root, "payUrl"),
            GetString(root, "deeplink"),
            GetString(root, "qrCodeUrl"),
            GetString(root, "message"),
            responseText,
            now);

        await _payments.AddAsync(payment, cancellationToken);
        await _payments.SaveChangesAsync(cancellationToken);

        return new MomoPaymentResponseDto(
            payment.Id,
            payment.OrderCode,
            requestId,
            payment.Amount,
            payment.Status,
            payment.PaymentUrl,
            payment.Deeplink,
            payment.QrCodeUrl,
            payment.ProviderMessage);
    }

    public async Task<PaymentDto> HandleMomoIpnAsync(
        MomoIpnDto request,
        CancellationToken cancellationToken = default)
    {
        EnsureMomoConfigured();
        if (string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.RequestId))
        {
            throw Validation("orderId", "MoMo orderId and requestId are required.");
        }

        var rawSignature = string.Join(
            "&",
            $"accessKey={_momoOptions.AccessKey}",
            $"amount={request.Amount}",
            $"extraData={request.ExtraData}",
            $"message={request.Message}",
            $"orderId={request.OrderId}",
            $"orderInfo={request.OrderInfo}",
            $"orderType={request.OrderType}",
            $"partnerCode={request.PartnerCode}",
            $"payType={request.PayType}",
            $"requestId={request.RequestId}",
            $"responseTime={request.ResponseTime}",
            $"resultCode={request.ResultCode}",
            $"transId={request.TransId}");

        EnsureSignature(rawSignature, _momoOptions.SecretKey, request.Signature, "signature");

        var payment = await _payments.GetByOrderCodeAsync(request.OrderId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), request.OrderId);
        var rawPayload = JsonSerializer.Serialize(request, JsonOptions);
        var now = _timeProvider.GetUtcNow();
        if (request.ResultCode == 0)
        {
            payment.MarkPaid(request.TransId.ToString(CultureInfo.InvariantCulture), request.Message, rawPayload, now);
            await ActivatePremiumSubscriptionIfNeededAsync(payment, now, cancellationToken);
        }
        else
        {
            payment.MarkFailed(request.Message, rawPayload, now);
        }

        await _payments.SaveChangesAsync(cancellationToken);
        return PaymentMapper.ToDto(payment);
    }

    public async Task<PayOsPaymentResponseDto> CreatePayOsPaymentAsync(
        CreatePayOsPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        return await CreatePayOsPaymentInternalAsync(
            request.Amount,
            request.Description,
            PaymentPurpose.General,
            null,
            cancellationToken);
    }

    public async Task<PayOsPaymentResponseDto> CreatePremiumPayOsPaymentAsync(
        CancellationToken cancellationToken = default)
    {
        EnsurePremiumConfigured();

        return await CreatePayOsPaymentInternalAsync(
            _premiumOptions.Price,
            BuildPremiumDescription(),
            PaymentPurpose.PremiumSubscription,
            _premiumOptions.Code,
            cancellationToken);
    }

    private async Task<PayOsPaymentResponseDto> CreatePayOsPaymentInternalAsync(
        decimal requestedAmount,
        string? requestedDescription,
        PaymentPurpose purpose,
        string? packageCode,
        CancellationToken cancellationToken)
    {
        EnsurePayOsConfigured();
        var userId = _userContext.GetRequiredUserId();
        var amount = ToWholeVnd(requestedAmount);
        var description = NormalizeDescription(requestedDescription);
        var orderCode = GeneratePayOsOrderCode();
        var now = _timeProvider.GetUtcNow();

        var rawSignature = string.Join(
            "&",
            $"amount={amount}",
            $"cancelUrl={_payOsOptions.CancelUrl}",
            $"description={description}",
            $"orderCode={orderCode}",
            $"returnUrl={_payOsOptions.ReturnUrl}");

        var payload = new
        {
            orderCode,
            amount,
            description,
            cancelUrl = _payOsOptions.CancelUrl,
            returnUrl = _payOsOptions.ReturnUrl,
            signature = Sign(rawSignature, _payOsOptions.ChecksumKey),
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _payOsOptions.Endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        httpRequest.Headers.Add("x-client-id", _payOsOptions.ClientId);
        httpRequest.Headers.Add("x-api-key", _payOsOptions.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw Validation("payos", responseText);
        }

        using var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        var payment = new PaymentTransaction(
            userId,
            PaymentMethod.PayOs,
            amount,
            orderCode.ToString(CultureInfo.InvariantCulture),
            description,
            now,
            purpose,
            packageCode);
        payment.AttachPayOsQrPayment(
            GetString(data, "qrCode"),
            null,
            responseText,
            now);
        payment.AttachMomoPayment(
            orderCode.ToString(CultureInfo.InvariantCulture),
            GetString(data, "checkoutUrl"),
            null,
            null,
            GetString(root, "desc"),
            responseText,
            now);

        await _payments.AddAsync(payment, cancellationToken);
        await _payments.SaveChangesAsync(cancellationToken);

        return new PayOsPaymentResponseDto(
            payment.Id,
            payment.OrderCode,
            payment.Amount,
            payment.Status,
            payment.PaymentUrl,
            payment.QrCode,
            payment.ProviderMessage);
    }

    public async Task<PaymentDto?> HandlePayOsWebhookAsync(
        PayOsWebhookDto request,
        CancellationToken cancellationToken = default)
    {
        EnsurePayOsConfigured();
        if (request.Data is null)
        {
            throw Validation("data", "PayOS webhook data is required.");
        }

        var rawSignature = BuildPayOsWebhookSignaturePayload(request.Data);
        EnsureSignature(rawSignature, _payOsOptions.ChecksumKey, request.Signature, "signature");

        var orderCode = request.Data.OrderCode.ToString(CultureInfo.InvariantCulture);
        var payment = await _payments.GetByOrderCodeAsync(orderCode, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        var rawPayload = JsonSerializer.Serialize(request, JsonOptions);
        var now = _timeProvider.GetUtcNow();
        if (request.Success && request.Code == "00" && request.Data.Code == "00")
        {
            payment.MarkPaid(request.Data.Reference, request.Data.Desc ?? request.Desc, rawPayload, now);
            await ActivatePremiumSubscriptionIfNeededAsync(payment, now, cancellationToken);
        }
        else
        {
            payment.MarkFailed(request.Data.Desc ?? request.Desc, rawPayload, now);
        }

        await _payments.SaveChangesAsync(cancellationToken);
        return PaymentMapper.ToDto(payment);
    }

    private async Task ActivatePremiumSubscriptionIfNeededAsync(
        PaymentTransaction payment,
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        if (payment.Purpose != PaymentPurpose.PremiumSubscription || payment.Status != PaymentStatus.Paid)
        {
            return;
        }

        EnsurePremiumConfigured();

        var existingSubscription = await _subscriptions.GetByPaymentTransactionIdAsync(payment.Id, cancellationToken);
        if (existingSubscription is not null)
        {
            return;
        }

        var currentPremium = await _subscriptions.GetActivePremiumAsync(payment.UserId, paidAt, cancellationToken);
        var startsAt = currentPremium is not null && currentPremium.ExpiresAt > paidAt
            ? currentPremium.ExpiresAt
            : paidAt;

        var subscription = UserSubscription.CreatePremium(
            payment.UserId,
            payment.PackageCode ?? _premiumOptions.Code,
            _premiumOptions.Name,
            payment.Id,
            startsAt,
            _premiumOptions.DurationDays,
            paidAt);

        await _subscriptions.AddAsync(subscription, cancellationToken);
    }

    private static string BuildPayOsWebhookSignaturePayload(PayOsWebhookDataDto data)
    {
        var fields = new SortedDictionary<string, string?>(StringComparer.Ordinal)
        {
            ["accountNumber"] = data.AccountNumber,
            ["amount"] = data.Amount.ToString(CultureInfo.InvariantCulture),
            ["code"] = data.Code,
            ["counterAccountBankId"] = data.CounterAccountBankId,
            ["counterAccountBankName"] = data.CounterAccountBankName,
            ["counterAccountName"] = data.CounterAccountName,
            ["counterAccountNumber"] = data.CounterAccountNumber,
            ["currency"] = data.Currency,
            ["desc"] = data.Desc,
            ["description"] = data.Description,
            ["orderCode"] = data.OrderCode.ToString(CultureInfo.InvariantCulture),
            ["paymentLinkId"] = data.PaymentLinkId,
            ["reference"] = data.Reference,
            ["transactionDateTime"] = data.TransactionDateTime,
            ["virtualAccountName"] = data.VirtualAccountName,
            ["virtualAccountNumber"] = data.VirtualAccountNumber,
        };

        return string.Join("&", fields.Select(field => $"{field.Key}={field.Value ?? string.Empty}"));
    }

    private static long GeneratePayOsOrderCode()
    {
        var unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return (unixMilliseconds % 9_000_000_000) + 1_000_000_000;
    }

    private static long ToWholeVnd(decimal amount)
    {
        if (amount <= 0 || decimal.Truncate(amount) != amount)
        {
            throw Validation("amount", "Amount must be a positive whole VND value.");
        }

        return decimal.ToInt64(amount);
    }

    private static string NormalizeDescription(string? description)
    {
        var normalized = string.IsNullOrWhiteSpace(description)
            ? "Thanh toan Homeji"
            : description.Trim();

        return normalized.Length <= PaymentTransaction.MaxDescriptionLength
            ? normalized
            : normalized[..PaymentTransaction.MaxDescriptionLength];
    }

    private string BuildPremiumDescription()
    {
        var packageName = string.IsNullOrWhiteSpace(_premiumOptions.Name)
            ? "Premium"
            : _premiumOptions.Name.Trim();

        return $"Homeji {packageName} {_premiumOptions.DurationDays} ngay";
    }

    private void EnsurePremiumConfigured()
    {
        if (string.IsNullOrWhiteSpace(_premiumOptions.Code)
            || string.IsNullOrWhiteSpace(_premiumOptions.Name)
            || _premiumOptions.Price <= 0
            || decimal.Truncate(_premiumOptions.Price) != _premiumOptions.Price
            || _premiumOptions.DurationDays <= 0)
        {
            throw new InvalidOperationException("Premium subscription settings are not configured.");
        }
    }

    private void EnsureMomoConfigured()
    {
        if (string.IsNullOrWhiteSpace(_momoOptions.PartnerCode)
            || string.IsNullOrWhiteSpace(_momoOptions.AccessKey)
            || string.IsNullOrWhiteSpace(_momoOptions.SecretKey)
            || string.IsNullOrWhiteSpace(_momoOptions.RedirectUrl)
            || string.IsNullOrWhiteSpace(_momoOptions.IpnUrl))
        {
            throw new InvalidOperationException("MoMo payment settings are not configured.");
        }
    }

    private void EnsurePayOsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_payOsOptions.ClientId)
            || string.IsNullOrWhiteSpace(_payOsOptions.ApiKey)
            || string.IsNullOrWhiteSpace(_payOsOptions.ChecksumKey)
            || string.IsNullOrWhiteSpace(_payOsOptions.ReturnUrl)
            || string.IsNullOrWhiteSpace(_payOsOptions.CancelUrl))
        {
            throw new InvalidOperationException("PayOS payment settings are not configured.");
        }
    }

    private static void EnsureSignature(string rawSignature, string secretKey, string? actualSignature, string field)
    {
        var expectedSignature = Sign(rawSignature, secretKey);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(actualSignature ?? string.Empty)))
        {
            throw Validation(field, "Payment signature is invalid.");
        }
    }

    private static string Sign(string rawSignature, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawSignature))).ToLowerInvariant();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind != JsonValueKind.Null
            ? property.GetString()
            : null;
    }

    private static RequestValidationException Validation(string field, string message)
    {
        return new RequestValidationException(new Dictionary<string, string[]>
        {
            [field] = [message],
        });
    }
}
