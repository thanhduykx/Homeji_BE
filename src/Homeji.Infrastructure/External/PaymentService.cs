using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IRepositories.Wallets;
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
    // payOS documents that transfer descriptions can be limited to 9 characters
    // when the receiving bank account is not linked through payOS.
    private const int PayOsDescriptionMaxLength = 9;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly UserContext _userContext;
    private readonly IPaymentTransactionRepository _payments;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IWalletRepository _wallets;
    private readonly MomoOptions _momoOptions;
    private readonly PayOsOptions _payOsOptions;
    private readonly PremiumSubscriptionOptions _premiumOptions;
    private readonly TimeProvider _timeProvider;

    public PaymentService(
        HttpClient httpClient,
        UserContext userContext,
        IPaymentTransactionRepository payments,
        IUserSubscriptionRepository subscriptions,
        IWalletRepository wallets,
        IOptions<MomoOptions> momoOptions,
        IOptions<PayOsOptions> payOsOptions,
        IOptions<PremiumSubscriptionOptions> premiumOptions,
        TimeProvider timeProvider)
    {
        _httpClient = httpClient;
        _userContext = userContext;
        _payments = payments;
        _subscriptions = subscriptions;
        _wallets = wallets;
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
            throw Validation("orderCode", "Mã đơn hàng là bắt buộc.");
        }

        var userId = _userContext.GetRequiredUserId();
        var payment = await _payments.GetByOrderCodeAsync(orderCode, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), orderCode);

        UserContext.EnsureOwner(userId, payment.UserId);
        return PaymentMapper.ToDto(payment);
    }

    public async Task<IReadOnlyList<PaymentDto>> GetMyPaymentHistoryAsync(
        PaymentStatus? status,
        int take,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var payments = await _payments.GetForUserAsync(userId, status, Math.Clamp(take, 1, 100), cancellationToken);
        return payments.Select(PaymentMapper.ToDto).ToArray();
    }

    public async Task<MomoPaymentResponseDto> CreateMomoPaymentAsync(
        CreateMomoPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        return await CreateMomoPaymentInternalAsync(
            request.Amount,
            request.Description ?? "Nap vi Homeji",
            PaymentPurpose.WalletTopUp,
            null,
            cancellationToken);
    }

    public async Task<MomoPaymentResponseDto> CreatePremiumMomoPaymentAsync(
        string packageCode,
        CancellationToken cancellationToken = default)
    {
        var plan = GetPremiumPlan(packageCode);

        return await CreateMomoPaymentInternalAsync(
            plan.Price,
            BuildPremiumDescription(plan),
            PaymentPurpose.PremiumSubscription,
            plan.Code,
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
        ValidateWalletTopUpAmount(purpose, amount);
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

        using var response = await SendAsync(
            () => _httpClient.PostAsJsonAsync(_momoOptions.Endpoint, payload, JsonOptions, cancellationToken),
            "MoMo",
            cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw ProviderFailure("MoMo", responseText);
        }

        using var document = ParseProviderResponse("MoMo", responseText);
        var root = document.RootElement;
        if (!TryGetInt32(root, "resultCode", out var resultCode) || resultCode != 0)
        {
            throw ProviderFailure("MoMo", GetString(root, "message") ?? "Yêu cầu thanh toán bị từ chối.");
        }

        var payUrl = GetString(root, "payUrl");
        if (!IsAbsoluteHttpsUrl(payUrl))
        {
            throw ProviderFailure("MoMo", "Phản hồi thành công nhưng không có đường dẫn thanh toán hợp lệ.");
        }

        var payment = new PaymentTransaction(userId, PaymentMethod.Momo, amount, orderCode, description, now, purpose, packageCode);
        payment.AttachMomoPayment(
            requestId,
            payUrl,
            GetString(root, "deeplink"),
            GetString(root, "qrCodeUrl"),
            GetString(root, "message"),
            responseText,
            now);

        await EnsureWalletExistsForTopUpAsync(purpose, userId, now, cancellationToken);
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
            throw Validation("orderId", "Cần có orderId và requestId từ MoMo.");
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
        if (payment.Method != PaymentMethod.Momo)
        {
            throw Validation("orderId", "Giao dịch tương ứng không phải thanh toán MoMo.");
        }

        if (!string.Equals(request.PartnerCode, _momoOptions.PartnerCode, StringComparison.Ordinal)
            || !string.Equals(request.RequestId, payment.RequestId, StringComparison.Ordinal))
        {
            throw Validation("requestId", "Callback MoMo không khớp với yêu cầu thanh toán.");
        }

        if (request.Amount != payment.Amount)
        {
            throw Validation("amount", "Số tiền callback MoMo không khớp giao dịch.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return PaymentMapper.ToDto(payment);
        }

        var rawPayload = JsonSerializer.Serialize(request, JsonOptions);
        var now = _timeProvider.GetUtcNow();
        if (request.ResultCode == 0)
        {
            payment.MarkPaid(request.TransId.ToString(CultureInfo.InvariantCulture), request.Message, rawPayload, now);
            await ActivatePremiumSubscriptionIfNeededAsync(payment, now, cancellationToken);
            await CreditWalletTopUpIfNeededAsync(payment, now, cancellationToken);
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
            request.Description ?? "Nap vi Homeji",
            PaymentPurpose.WalletTopUp,
            null,
            cancellationToken);
    }

    public async Task<PayOsPaymentResponseDto> CreatePremiumPayOsPaymentAsync(
        string packageCode,
        CancellationToken cancellationToken = default)
    {
        var plan = GetPremiumPlan(packageCode);

        return await CreatePayOsPaymentInternalAsync(
            plan.Price,
            BuildPremiumDescription(plan),
            PaymentPurpose.PremiumSubscription,
            plan.Code,
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
        ValidateWalletTopUpAmount(purpose, amount);
        var description = NormalizeDescription(requestedDescription);
        var orderCode = GeneratePayOsOrderCode();
        var providerDescription = BuildPayOsDescription(orderCode);
        var now = _timeProvider.GetUtcNow();

        var rawSignature = string.Join(
            "&",
            $"amount={amount}",
            $"cancelUrl={_payOsOptions.CancelUrl}",
            $"description={providerDescription}",
            $"orderCode={orderCode}",
            $"returnUrl={_payOsOptions.ReturnUrl}");

        var payload = new
        {
            orderCode,
            amount,
            description = providerDescription,
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

        using var response = await SendAsync(
            () => _httpClient.SendAsync(httpRequest, cancellationToken),
            "PayOS",
            cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw ProviderFailure("PayOS", responseText);
        }

        using var document = ParseProviderResponse("PayOS", responseText);
        var root = document.RootElement;
        var providerCode = GetString(root, "code");
        var providerMessage = GetString(root, "desc");
        if (!string.Equals(providerCode, "00", StringComparison.Ordinal)
            || !root.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Object)
        {
            throw ProviderFailure(
                "PayOS",
                $"Payment link request was rejected (code {providerCode ?? "unknown"}): {providerMessage ?? "Không có chi tiết trả về."}");
        }

        var checkoutUrl = GetString(data, "checkoutUrl");
        var qrCode = GetString(data, "qrCode");
        var providerStatus = GetString(data, "status");
        if (!TryGetInt64(data, "orderCode", out var responseOrderCode)
            || responseOrderCode != orderCode
            || !TryGetInt64(data, "amount", out var responseAmount)
            || responseAmount != amount
            || !string.Equals(providerStatus, "PENDING", StringComparison.OrdinalIgnoreCase)
            || !IsAbsoluteHttpsUrl(checkoutUrl)
            || string.IsNullOrWhiteSpace(qrCode))
        {
            throw ProviderFailure("PayOS", "Phản hồi thành công nhưng dữ liệu link thanh toán không hợp lệ.");
        }

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
            qrCode,
            null,
            responseText,
            now);
        payment.AttachMomoPayment(
            orderCode.ToString(CultureInfo.InvariantCulture),
            checkoutUrl,
            null,
            null,
            providerMessage,
            responseText,
            now);

        await EnsureWalletExistsForTopUpAsync(purpose, userId, now, cancellationToken);
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
            throw Validation("data", "Dữ liệu webhook PayOS là bắt buộc.");
        }

        var rawSignature = BuildPayOsWebhookSignaturePayload(request.Data);
        EnsureSignature(rawSignature, _payOsOptions.ChecksumKey, request.Signature, "signature");

        var orderCode = request.Data.OrderCode.ToString(CultureInfo.InvariantCulture);
        var payment = await _payments.GetByOrderCodeAsync(orderCode, cancellationToken);
        if (payment is null)
        {
            return null;
        }

        if (payment.Method != PaymentMethod.PayOs)
        {
            throw Validation("orderCode", "Giao dịch tương ứng không phải thanh toán PayOS.");
        }

        if (request.Data.Amount != payment.Amount)
        {
            throw Validation("amount", "Số tiền webhook PayOS không khớp giao dịch.");
        }

        if (!string.Equals(request.Data.Currency, "VND", StringComparison.OrdinalIgnoreCase))
        {
            throw Validation("currency", "Tiền tệ webhook PayOS phải là VND.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return PaymentMapper.ToDto(payment);
        }

        var rawPayload = JsonSerializer.Serialize(request, JsonOptions);
        var now = _timeProvider.GetUtcNow();
        if (request.Success && request.Code == "00" && request.Data.Code == "00")
        {
            payment.MarkPaid(request.Data.Reference, request.Data.Desc ?? request.Desc, rawPayload, now);
            await ActivatePremiumSubscriptionIfNeededAsync(payment, now, cancellationToken);
            await CreditWalletTopUpIfNeededAsync(payment, now, cancellationToken);
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

        var plan = GetPremiumPlan(payment.PackageCode);

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
            plan.Code,
            plan.Name,
            payment.Id,
            startsAt,
            plan.DurationDays,
            paidAt);

        await _subscriptions.AddAsync(subscription, cancellationToken);
    }

    private async Task CreditWalletTopUpIfNeededAsync(
        PaymentTransaction payment,
        DateTimeOffset paidAt,
        CancellationToken cancellationToken)
    {
        if (payment.Purpose != PaymentPurpose.WalletTopUp || payment.Status != PaymentStatus.Paid)
        {
            return;
        }

        if (await _wallets.HasTransactionAsync(
            payment.UserId,
            WalletTransactionKind.TopUp,
            payment.Id,
            cancellationToken))
        {
            return;
        }

        var wallet = await _wallets.GetAsync(payment.UserId, cancellationToken);
        if (wallet is null)
        {
            wallet = WalletAccount.Create(payment.UserId, paidAt);
            await _wallets.AddAccountAsync(wallet, cancellationToken);
        }

        wallet.CreditTopUp(payment.Amount, paidAt);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            payment.UserId,
            WalletTransactionKind.TopUp,
            payment.Amount,
            wallet.Balance,
            payment.Id,
            $"Nạp ví qua {payment.Method}",
            paidAt), cancellationToken);
    }

    private async Task EnsureWalletExistsForTopUpAsync(
        PaymentPurpose purpose,
        Guid userId,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken)
    {
        if (purpose != PaymentPurpose.WalletTopUp
            || await _wallets.GetAsync(userId, cancellationToken) is not null)
        {
            return;
        }

        await _wallets.AddAccountAsync(WalletAccount.Create(userId, createdAt), cancellationToken);
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
            throw Validation("amount", "Số tiền phải là số nguyên VND dương.");
        }

        return decimal.ToInt64(amount);
    }

    private static void ValidateWalletTopUpAmount(PaymentPurpose purpose, long amount)
    {
        if (purpose != PaymentPurpose.WalletTopUp)
        {
            return;
        }

        if (amount < WalletAccount.MinimumTopUp || amount > WalletAccount.MaximumTopUp)
        {
            throw Validation(
                "amount",
                $"Wallet top-up must be between {WalletAccount.MinimumTopUp:0} and {WalletAccount.MaximumTopUp:0} VND.");
        }
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

    private static string BuildPremiumDescription(PremiumPlanOptions plan)
    {
        var packageName = plan.Name.Trim();

        return $"Homeji {packageName} {plan.DurationDays} ngay";
    }

    private static string BuildPayOsDescription(long orderCode)
    {
        var suffix = orderCode.ToString(CultureInfo.InvariantCulture);
        suffix = suffix.Length <= PayOsDescriptionMaxLength - 2
            ? suffix
            : suffix[^(PayOsDescriptionMaxLength - 2)..];

        return $"HJ{suffix}";
    }

    private PremiumPlanOptions GetPremiumPlan(string? packageCode)
    {
        if (string.IsNullOrWhiteSpace(packageCode))
        {
            throw Validation("packageCode", "Mã gói Premium là bắt buộc.");
        }

        var plan = _premiumOptions.GetPlans().SingleOrDefault(candidate =>
            string.Equals(candidate.Code, packageCode.Trim(), StringComparison.OrdinalIgnoreCase));

        if (plan is null)
        {
            throw Validation("packageCode", "Không tìm thấy gói Premium.");
        }

        if (string.IsNullOrWhiteSpace(plan.Code)
            || string.IsNullOrWhiteSpace(plan.Name)
            || plan.Price <= 0
            || decimal.Truncate(plan.Price) != plan.Price
            || plan.DurationDays <= 0)
        {
            throw new InvalidOperationException("Chưa cấu hình gói Premium.");
        }

        return plan;
    }

    private void EnsureMomoConfigured()
    {
        if (string.IsNullOrWhiteSpace(_momoOptions.PartnerCode)
            || string.IsNullOrWhiteSpace(_momoOptions.AccessKey)
            || string.IsNullOrWhiteSpace(_momoOptions.SecretKey)
            || string.IsNullOrWhiteSpace(_momoOptions.RedirectUrl)
            || string.IsNullOrWhiteSpace(_momoOptions.IpnUrl))
        {
            throw new InvalidOperationException("Chưa cấu hình thanh toán MoMo.");
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
            throw new InvalidOperationException("Chưa cấu hình thanh toán PayOS.");
        }
    }

    private static void EnsureSignature(string rawSignature, string secretKey, string? actualSignature, string field)
    {
        var expectedSignature = Sign(rawSignature, secretKey);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(actualSignature ?? string.Empty)))
        {
            throw Validation(field, "Chữ ký thanh toán không hợp lệ.");
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

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out value);
    }

    private static bool TryGetInt64(JsonElement element, string propertyName, out long value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt64(out value);
    }

    private static bool IsAbsoluteHttpsUrl(string? value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }

    private static JsonDocument ParseProviderResponse(string provider, string responseText)
    {
        try
        {
            return JsonDocument.Parse(responseText);
        }
        catch (JsonException exception)
        {
            throw new ExternalDependencyException($"{provider} trả về phản hồi không hợp lệ.", exception);
        }
    }

    private static async Task<HttpResponseMessage> SendAsync(
        Func<Task<HttpResponseMessage>> send,
        string provider,
        CancellationToken cancellationToken)
    {
        try
        {
            return await send();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(provider, $"{provider} hết thời gian chờ yêu cầu thanh toán.", TimeSpan.FromSeconds(5));
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalDependencyException($"Không thể kết nối tới {provider}.", exception);
        }
    }

    private static ExternalDependencyException ProviderFailure(string provider, string details)
    {
        var normalizedDetails = string.IsNullOrWhiteSpace(details)
            ? "Không có chi tiết trả về."
            : details.Trim();
        if (normalizedDetails.Length > PaymentTransaction.MaxProviderMessageLength)
        {
            normalizedDetails = normalizedDetails[..PaymentTransaction.MaxProviderMessageLength];
        }

        return new ExternalDependencyException($"{provider} thanh toán thất bại: {normalizedDetails}");
    }

    private static RequestValidationException Validation(string field, string message)
    {
        return new RequestValidationException(new Dictionary<string, string[]>
        {
            [field] = [message],
        });
    }
}
