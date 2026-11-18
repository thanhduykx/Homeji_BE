using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Emails;
using Homeji.Application.IServices.Payments;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.Mappers.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class PayPalPaymentService(
    HttpClient http,
    UserContext userContext,
    IPaymentTransactionRepository payments,
    IUserSubscriptionRepository subscriptions,
    IWalletRepository wallets,
    ITransactionReceiptService receipts,
    IPayPalExchangeRateService exchangeRates,
    IOptions<PayPalOptions> payPalOptions,
    IOptions<PremiumSubscriptionOptions> premiumOptions,
    TimeProvider timeProvider,
    IPremiumPricing? pricing = null,
    ApplicationDbContext? dbContext = null) : IPayPalPaymentService
{
    private const string Currency = "USD";
    private const int MaxPayPalItemDescriptionLength = 120;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PayPalOptions _options = payPalOptions.Value;

    public async Task<PayPalCheckoutConfigDto> GetConfigAndQuoteAsync(
        decimal? amountVnd,
        CancellationToken cancellationToken = default)
    {
        userContext.GetRequiredUserId();
        var quote = amountVnd.HasValue
            ? await exchangeRates.QuoteUsdAsync(amountVnd.Value, cancellationToken)
            : null;
        return BuildConfig(quote);
    }

    public Task<PayPalPaymentResponseDto> CreateWalletTopUpAsync(
        CreatePayPalPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        return CreatePaymentInternalAsync(
            request.Amount,
            request.Description ?? "Nạp ví Homeji",
            PaymentPurpose.WalletTopUp,
            null,
            cancellationToken);
    }

    public async Task<PayPalPaymentResponseDto> CreatePremiumPaymentAsync(
        string packageCode,
        decimal? expectedAmount = null,
        CancellationToken cancellationToken = default)
    {
        var plan = await GetPremiumPlanAsync(packageCode, cancellationToken);
        if (expectedAmount.HasValue && plan.Price != expectedAmount.Value)
        {
            throw Validation("expectedAmount", plan.OfferUsed
                ? "Bạn đã dùng ưu đãi của đợt này. Bạn vẫn có thể gia hạn với giá thường; hãy tải lại bảng giá để tiếp tục."
                : "Giá gói đã thay đổi. Vui lòng tải lại và xác nhận giá mới.");
        }

        return await CreatePaymentInternalAsync(
            plan.Price,
            $"Homeji {plan.Name.Trim()} {plan.DurationDays} ngay",
            PaymentPurpose.PremiumSubscription,
            plan.Code,
            cancellationToken);
    }

    public async Task<PayPalCheckoutConfigDto> GetPaymentWalletsAsync(
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetRequiredUserId();
        await payments.CancelOverdueAsync(timeProvider.GetUtcNow(), userId, cancellationToken);
        var payment = await payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), paymentId);
        UserContext.EnsureOwner(userId, payment.UserId);
        EnsurePayPalPayment(payment);

        var quote = TryReadStoredQuote(payment.RawProviderPayload)
            ?? await exchangeRates.QuoteUsdAsync(payment.Amount, cancellationToken);
        return BuildConfig(quote);
    }

    public async Task<PayPalWalletOrderDto> CreatePaymentWalletOrderAsync(
        Guid paymentId,
        string wallet,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetRequiredUserId();
        await userContext.GetRequiredProfileAsync(cancellationToken);
        if (!IsWalletEnabled(wallet))
        {
            throw new NotFoundException("PayPalWallet", wallet);
        }

        EnsureConfigured();
        await payments.CancelOverdueAsync(timeProvider.GetUtcNow(), userId, cancellationToken);
        var payment = await payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), paymentId);
        UserContext.EnsureOwner(userId, payment.UserId);
        EnsurePayPalPayment(payment);
        if (payment.Status != PaymentStatus.Pending)
        {
            throw new ConflictException("Giao dịch không còn ở trạng thái chờ thanh toán.");
        }

        var quote = TryReadStoredQuote(payment.RawProviderPayload)
            ?? await exchangeRates.QuoteUsdAsync(payment.Amount, cancellationToken);
        var totalUsdText = FormatUsd(quote.TotalUsd);
        var order = await CreateOrderAsync(new
        {
            intent = "CAPTURE",
            purchase_units = BuildPurchaseUnits(payment.OrderCode, payment.Description, totalUsdText),
            application_context = new { shipping_preference = "NO_SHIPPING" },
        }, Guid.NewGuid().ToString(), cancellationToken);

        var ticket = ProtectTicket(userId, payment.Id, wallet, order.Id!, quote.TotalUsd);
        return new PayPalWalletOrderDto(order.Id!, totalUsdText, ticket);
    }

    public async Task<PaymentDto> CapturePaymentAsync(
        Guid paymentId,
        CapturePayPalPaymentDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetRequiredUserId();
        EnsureConfigured();

        var payment = await payments.GetByIdAsync(paymentId, cancellationToken)
            ?? throw new NotFoundException(nameof(PaymentTransaction), paymentId);
        UserContext.EnsureOwner(userId, payment.UserId);
        EnsurePayPalPayment(payment);

        if (payment.Status == PaymentStatus.Paid)
        {
            return PaymentMapper.ToDto(payment);
        }

        if (payment.Status is not (PaymentStatus.Pending or PaymentStatus.Cancelled))
        {
            return PaymentMapper.ToDto(payment);
        }

        var quote = TryReadStoredQuote(payment.RawProviderPayload)
            ?? throw ProviderError();

        string? wallet;
        string payPalOrderId;
        decimal expectedUsd;

        if (!string.IsNullOrWhiteSpace(request.Ticket))
        {
            (wallet, payPalOrderId, expectedUsd) = UnprotectTicket(userId, payment.Id, request.Ticket);
        }
        else
        {
            wallet = null;
            if (!string.IsNullOrWhiteSpace(request.PayPalOrderId)
                && !string.Equals(request.PayPalOrderId.Trim(), payment.RequestId, StringComparison.Ordinal))
            {
                throw Validation("payPalOrderId", "Mã đơn PayPal không khớp với giao dịch.");
            }

            if (!IsOrderId(payment.RequestId))
            {
                throw Validation("payPalOrderId", "Giao dịch chưa có mã đơn PayPal hợp lệ.");
            }

            payPalOrderId = payment.RequestId!;
            expectedUsd = quote.TotalUsd;
        }

        var token = await GetTokenAsync(cancellationToken);
        var orderPath = $"v2/checkout/orders/{payPalOrderId}";
        var currentOrder = await GetOrderAsync(orderPath, token, cancellationToken);
        if (!string.Equals(currentOrder.Id, payPalOrderId, StringComparison.Ordinal))
        {
            throw ProviderError();
        }

        if (currentOrder.Status is not ("APPROVED" or "COMPLETED"))
        {
            return PaymentMapper.ToDto(payment);
        }

        if (wallet is not null && !IsAuthorizedWalletSource(wallet, currentOrder.PaymentSource))
        {
            return await MarkFailedAsync(
                payment.OrderCode,
                "Ví chưa xác thực được giao dịch PayPal.",
                SerializeStoredPayload(new StoredPayPalPayload(quote, currentOrder.RawJson)),
                cancellationToken);
        }

        var finalOrder = currentOrder;
        if (!string.Equals(currentOrder.Status, "COMPLETED", StringComparison.Ordinal))
        {
            using var captureRequest = OrderRequest(HttpMethod.Post, $"{orderPath}/capture", token);
            captureRequest.Headers.Add("PayPal-Request-Id", $"capture-{payPalOrderId}");
            captureRequest.Headers.Add("Prefer", "return=representation");
            captureRequest.Content = JsonContent.Create(new { }, options: JsonOptions);
            using var captureResponse = await SendAsync(captureRequest, cancellationToken);

            finalOrder = captureResponse.StatusCode == HttpStatusCode.UnprocessableEntity
                ? await GetOrderAsync(orderPath, token, cancellationToken)
                : await ReadOrderAsync(captureResponse, cancellationToken);
        }

        if (!string.Equals(finalOrder.Id, payPalOrderId, StringComparison.Ordinal))
        {
            throw ProviderError();
        }

        var capture = finalOrder.PurchaseUnits is [{ Payments.Captures: [var singleCapture] }]
            ? singleCapture
            : null;
        if (capture is null)
        {
            return PaymentMapper.ToDto(payment);
        }

        if (capture.Amount is null
            || !string.Equals(capture.Amount.Currency, Currency, StringComparison.OrdinalIgnoreCase)
            || !decimal.TryParse(capture.Amount.Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var capturedUsd)
            || capturedUsd != expectedUsd
            || string.IsNullOrWhiteSpace(capture.Id))
        {
            throw ProviderError();
        }

        var finalPayload = SerializeStoredPayload(new StoredPayPalPayload(quote, finalOrder.RawJson));
        if (capture.Status is "DECLINED" or "FAILED" or "REFUNDED" or "PARTIALLY_REFUNDED")
        {
            return await MarkFailedAsync(
                payment.OrderCode,
                "PayPal báo giao dịch không thành công.",
                finalPayload,
                cancellationToken);
        }

        if (!string.Equals(capture.Status, "COMPLETED", StringComparison.Ordinal)
            || !string.Equals(finalOrder.Status, "COMPLETED", StringComparison.Ordinal))
        {
            return PaymentMapper.ToDto(payment);
        }

        var paidAt = timeProvider.GetUtcNow();
        var paidMessage = BuildPaidMessage(quote, wallet);
        return await payments.ExecuteForOrderAsync(payment.OrderCode, async () =>
        {
            var current = await payments.GetByOrderCodeAsync(payment.OrderCode, cancellationToken) ?? payment;
            if (current.Status == PaymentStatus.Paid)
            {
                return PaymentMapper.ToDto(current);
            }

            if (current.Amount != quote.AmountVnd)
            {
                throw ProviderError();
            }

            current.MarkPaid(capture.Id, paidMessage, finalPayload, paidAt);
            await ActivatePremiumSubscriptionIfNeededAsync(current, paidAt, cancellationToken);
            await CreditWalletTopUpIfNeededAsync(current, paidAt, cancellationToken);
            await receipts.RecordPaymentAsync(current, cancellationToken);
            await payments.SaveChangesAsync(cancellationToken);
            return PaymentMapper.ToDto(current);
        }, cancellationToken);
    }

    private async Task<PayPalPaymentResponseDto> CreatePaymentInternalAsync(
        decimal requestedAmount,
        string? requestedDescription,
        PaymentPurpose purpose,
        string? packageCode,
        CancellationToken cancellationToken)
    {
        var userId = userContext.GetRequiredUserId();
        await userContext.GetRequiredProfileAsync(cancellationToken);
        EnsureConfigured();

        var amount = ToWholeVnd(requestedAmount);
        ValidateWalletTopUpAmount(purpose, amount);
        var origin = GetValidatedOrigin();
        var quote = await exchangeRates.QuoteUsdAsync(amount, cancellationToken);
        var description = NormalizeDescription(requestedDescription);
        var now = timeProvider.GetUtcNow();
        var orderCode = $"PP{Guid.NewGuid():N}";
        var payload = SerializeStoredPayload(new StoredPayPalPayload(quote));
        var quoteMessage = BuildQuoteMessage(quote);

        var payment = new PaymentTransaction(
            userId,
            PaymentMethod.PayPal,
            amount,
            orderCode,
            description,
            now,
            purpose,
            packageCode);
        payment.AttachMomoPayment(orderCode, null, null, null, quoteMessage, payload, now);

        await EnsureWalletExistsForTopUpAsync(purpose, userId, now, cancellationToken);
        if (purpose == PaymentPurpose.PremiumSubscription)
        {
            await AddPremiumPendingPaymentAsync(payment, now, cancellationToken);
        }
        else
        {
            await payments.AddAsync(payment, cancellationToken);
            await payments.SaveChangesAsync(cancellationToken);
        }

        var returnUrl = new Uri(origin, $"/payments/wait?orderCode={Uri.EscapeDataString(orderCode)}&provider=paypal");
        var cancelUrl = new Uri(origin, $"/payments/wait?orderCode={Uri.EscapeDataString(orderCode)}&provider=paypal&cancel=true");
        Order order;
        try
        {
            order = await CreateOrderAsync(new
            {
                intent = "CAPTURE",
                purchase_units = BuildPurchaseUnits(orderCode, description, FormatUsd(quote.TotalUsd)),
                payment_source = new
                {
                    paypal = new
                    {
                        experience_context = new
                        {
                            return_url = returnUrl.AbsoluteUri,
                            cancel_url = cancelUrl.AbsoluteUri,
                            shipping_preference = "NO_SHIPPING",
                            user_action = "PAY_NOW",
                        },
                    },
                },
            }, orderCode, cancellationToken);
        }
        catch
        {
            await MarkFailedAsync(orderCode, "Không tạo được đơn thanh toán trên PayPal.", payload, CancellationToken.None);
            throw;
        }

        var approval = order.Links?.FirstOrDefault(link => link.Rel is "payer-action" or "approve");
        if (!Uri.TryCreate(approval?.Href, UriKind.Absolute, out var approvalUrl)
            || approvalUrl.Scheme != Uri.UriSchemeHttps
            || !string.Equals(approvalUrl.Host, _options.CheckoutHost, StringComparison.OrdinalIgnoreCase)
            || !approvalUrl.IsDefaultPort
            || !string.IsNullOrEmpty(approvalUrl.UserInfo))
        {
            await MarkFailedAsync(orderCode, "PayPal chưa trả về liên kết thanh toán hợp lệ.", payload, CancellationToken.None);
            throw ProviderError();
        }

        payment = await payments.ExecuteForOrderAsync(orderCode, async () =>
        {
            var current = await payments.GetByOrderCodeAsync(orderCode, cancellationToken) ?? payment;
            current.AttachMomoPayment(
                order.Id!,
                approvalUrl.AbsoluteUri,
                null,
                null,
                current.Status == PaymentStatus.Paid ? current.ProviderMessage : quoteMessage,
                current.Status == PaymentStatus.Paid ? current.RawProviderPayload : payload,
                current.Status == PaymentStatus.Paid ? current.UpdatedAt : now);
            await payments.SaveChangesAsync(cancellationToken);
            return current;
        }, cancellationToken);

        return new PayPalPaymentResponseDto(
            payment.Id,
            payment.OrderCode,
            order.Id!,
            payment.Amount,
            payment.Status,
            payment.PaymentUrl,
            payment.ProviderMessage,
            quote);
    }

    private async Task<PaymentDto> MarkFailedAsync(
        string orderCode,
        string message,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        return await payments.ExecuteForOrderAsync(orderCode, async () =>
        {
            var current = await payments.GetByOrderCodeAsync(orderCode, cancellationToken);
            if (current is null)
            {
                throw new NotFoundException(nameof(PaymentTransaction), orderCode);
            }

            current.MarkFailed(message, rawPayload, now);
            await payments.SaveChangesAsync(cancellationToken);
            return PaymentMapper.ToDto(current);
        }, cancellationToken);
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

        var plan = await GetPremiumPlanAsync(payment.PackageCode, cancellationToken);
        var existingSubscription = await subscriptions.GetByPaymentTransactionIdAsync(payment.Id, cancellationToken);
        if (existingSubscription is not null)
        {
            return;
        }

        var currentPremium = await subscriptions.GetPremiumTailAsync(payment.UserId, paidAt, cancellationToken);
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

        await subscriptions.AddAsync(subscription, cancellationToken);
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

        if (await wallets.HasTransactionAsync(
            payment.UserId,
            WalletTransactionKind.TopUp,
            payment.Id,
            cancellationToken))
        {
            return;
        }

        var wallet = await wallets.GetAsync(payment.UserId, cancellationToken);
        if (wallet is null)
        {
            wallet = WalletAccount.Create(payment.UserId, paidAt);
            await wallets.AddAccountAsync(wallet, cancellationToken);
        }

        wallet.CreditTopUp(payment.Amount, paidAt);
        await wallets.AddTransactionAsync(new WalletTransaction(
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
            || await wallets.GetAsync(userId, cancellationToken) is not null)
        {
            return;
        }

        await wallets.AddAccountAsync(WalletAccount.Create(userId, createdAt), cancellationToken);
    }

    private async Task AddPremiumPendingPaymentAsync(
        PaymentTransaction payment,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (dbContext is null)
        {
            await payments.AddAsync(payment, cancellationToken);
            await payments.SaveChangesAsync(cancellationToken);
            return;
        }

        var userId = payment.UserId;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = userId.ToString("N");
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        await payments.CancelOverdueAsync(now, userId, cancellationToken);

        if (pricing is not null)
        {
            var currentPlan = await GetPremiumPlanAsync(payment.PackageCode, cancellationToken);
            if (currentPlan.Price != payment.Amount)
            {
                throw Validation("expectedAmount", currentPlan.OfferUsed
                    ? "Bạn đã dùng ưu đãi của đợt này. Bạn vẫn có thể gia hạn với giá thường; hãy tải lại bảng giá để tiếp tục."
                    : "Giá gói đã thay đổi. Vui lòng tải lại và xác nhận giá mới.");
            }
        }

        var hasPending = await dbContext.PaymentTransactions.AsNoTracking()
            .AnyAsync(existing => existing.UserId == userId
                && existing.Purpose == PaymentPurpose.PremiumSubscription
                && existing.PackageCode == payment.PackageCode
                && existing.Status == PaymentStatus.Pending,
                cancellationToken);
        if (hasPending)
        {
            throw new ConflictException("Bạn đã có một đơn Premium đang chờ thanh toán. Hoàn tất hoặc chờ đơn hết hạn rồi hãy thử lại.");
        }

        await payments.AddAsync(payment, cancellationToken);
        await payments.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<PremiumPlanOptions> GetPremiumPlanAsync(
        string? packageCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(packageCode))
        {
            throw Validation("packageCode", "Mã gói Premium là bắt buộc.");
        }

        var plans = pricing is null
            ? premiumOptions.Value.GetPlansAt(timeProvider.GetUtcNow())
            : await pricing.GetPlansAsync(cancellationToken);
        var plan = plans.SingleOrDefault(candidate =>
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

    private PayPalCheckoutConfigDto BuildConfig(PayPalCurrencyQuoteDto? quote)
    {
        var configured = _options.IsConfigured;
        return new PayPalCheckoutConfigDto(
            configured,
            _options.IsLive ? "live" : "sandbox",
            configured ? _options.ClientId : null,
            configured && _options.GooglePayEnabled,
            configured && IsApplePayEnabled(),
            quote);
    }

    private bool IsWalletEnabled(string wallet) => wallet switch
    {
        "google-pay" => _options.GooglePayEnabled,
        "apple-pay" => IsApplePayEnabled(),
        _ => false,
    };

    private bool IsApplePayEnabled() => _options.ApplePayEnabled
        && Uri.TryCreate(_options.FrontendOrigin, UriKind.Absolute, out var origin)
        && origin.Scheme == Uri.UriSchemeHttps
        && !origin.IsLoopback;

    private Uri GetValidatedOrigin()
    {
        if (!Uri.TryCreate(_options.FrontendOrigin, UriKind.Absolute, out var origin)
            || origin.AbsolutePath != "/"
            || !string.IsNullOrEmpty(origin.UserInfo)
            || !string.IsNullOrEmpty(origin.Query)
            || !string.IsNullOrEmpty(origin.Fragment)
            || origin.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Chưa cấu hình FrontendOrigin hợp lệ cho PayPal.");
        }

        return origin;
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new InvalidOperationException("Chưa cấu hình thanh toán PayPal.");
        }
    }

    private static void EnsurePayPalPayment(PaymentTransaction payment)
    {
        if (payment.Method != PaymentMethod.PayPal)
        {
            throw Validation("paymentId", "Giao dịch này không sử dụng PayPal.");
        }
    }

    private async Task<Order> CreateOrderAsync<T>(
        T body,
        string requestId,
        CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        using var request = OrderRequest(HttpMethod.Post, "v2/checkout/orders", token);
        request.Headers.Add("PayPal-Request-Id", requestId);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await SendAsync(request, cancellationToken);
        var order = await ReadOrderAsync(response, cancellationToken);
        return IsOrderId(order.Id) ? order : throw ProviderError();
    }

    private async Task<Order> GetOrderAsync(
        string orderPath,
        string token,
        CancellationToken cancellationToken)
    {
        using var request = OrderRequest(HttpMethod.Get, orderPath, token);
        using var response = await SendAsync(request, cancellationToken);
        return await ReadOrderAsync(response, cancellationToken);
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.ApiBaseUri, "v1/oauth2/token"));
        var credentials = Convert.ToBase64String(
            Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
        });
        using var response = await SendAsync(request, cancellationToken);
        var token = await ReadAsync<OAuthToken>(response, cancellationToken);
        return !string.IsNullOrWhiteSpace(token.AccessToken)
            ? token.AccessToken
            : throw ProviderError();
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new ExternalDependencyException("Không kết nối được với cổng PayPal.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalDependencyException("PayPal phản hồi quá chậm. Vui lòng thử lại.", exception);
        }
    }

    private static async Task<Order> ReadOrderAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw ProviderError();
        }

        var rawJson = await response.Content.ReadAsStringAsync(cancellationToken);
        try
        {
            var order = JsonSerializer.Deserialize<Order>(rawJson, JsonOptions)
                ?? throw ProviderError();
            return order with { RawJson = rawJson };
        }
        catch (JsonException)
        {
            throw ProviderError();
        }
    }

    private static async Task<T> ReadAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw ProviderError();
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken)
                ?? throw ProviderError();
        }
        catch (JsonException)
        {
            throw ProviderError();
        }
    }

    private HttpRequestMessage OrderRequest(HttpMethod method, string relativePath, string token)
    {
        var request = new HttpRequestMessage(method, new Uri(_options.ApiBaseUri, relativePath));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private string ProtectTicket(
        Guid userId,
        Guid paymentId,
        string wallet,
        string payPalOrderId,
        decimal totalUsd)
    {
        var expiresAt = timeProvider.GetUtcNow().AddHours(1);
        var payload = string.Join(
            '|',
            userId.ToString("N"),
            paymentId.ToString("N"),
            wallet,
            payPalOrderId,
            FormatUsd(totalUsd),
            expiresAt.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture));
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        var signature = SignTicket(encoded);
        return $"{encoded}.{signature}";
    }

    private (string Wallet, string PayPalOrderId, decimal ExpectedUsd) UnprotectTicket(
        Guid userId,
        Guid paymentId,
        string ticket)
    {
        var parts = ticket.Trim().Split('.');
        if (parts.Length != 2)
        {
            throw Validation("ticket", "Phiên thanh toán ví đã hết hạn hoặc không hợp lệ.");
        }

        var expectedSignature = SignTicket(parts[0]);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expectedSignature),
                Encoding.UTF8.GetBytes(parts[1])))
        {
            throw Validation("ticket", "Phiên thanh toán ví đã hết hạn hoặc không hợp lệ.");
        }

        string payload;
        try
        {
            payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
        }
        catch (FormatException)
        {
            throw Validation("ticket", "Phiên thanh toán ví đã hết hạn hoặc không hợp lệ.");
        }

        var fields = payload.Split('|');
        if (fields.Length != 6
            || !Guid.TryParseExact(fields[0], "N", out var ticketUserId)
            || ticketUserId != userId
            || !Guid.TryParseExact(fields[1], "N", out var ticketPaymentId)
            || ticketPaymentId != paymentId
            || !IsWalletEnabled(fields[2])
            || !IsOrderId(fields[3])
            || !decimal.TryParse(fields[4], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var expectedUsd)
            || expectedUsd <= 0
            || !long.TryParse(fields[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var expiresUnix)
            || timeProvider.GetUtcNow().ToUnixTimeSeconds() > expiresUnix)
        {
            throw Validation("ticket", "Phiên thanh toán ví đã hết hạn hoặc không hợp lệ.");
        }

        return (fields[2], fields[3], expectedUsd);
    }

    private string SignTicket(string encodedPayload)
    {
        var key = Encoding.UTF8.GetBytes($"Homeji.PayPal.Ticket.v1:{_options.ClientSecret}");
        using var hmac = new HMACSHA256(key);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(encodedPayload))).ToLowerInvariant();
    }

    private static bool IsAuthorizedWalletSource(string wallet, PaymentSource? paymentSource) => wallet switch
    {
        "apple-pay" => paymentSource?.ApplePay is not null,
        "google-pay" => paymentSource?.GooglePay is { } googlePay
            && CanCaptureGooglePay(googlePay.Card?.AuthenticationResult),
        _ => false,
    };

    private static bool CanCaptureGooglePay(AuthenticationResult? authentication) => authentication is
        null or { LiabilityShift: "POSSIBLE" }
        or { LiabilityShift: "NO", ThreeDSecure: { EnrollmentStatus: "N" or "U" or "B", AuthenticationStatus: null } };

    private static object[] BuildPurchaseUnits(string orderCode, string description, string totalUsd) =>
    [
        new
        {
            reference_id = orderCode,
            custom_id = orderCode,
            description = BuildPayPalItemDescription(description),
            amount = new { currency_code = Currency, value = totalUsd },
        },
    ];

    private static string BuildPayPalItemDescription(string description)
    {
        var trimmed = string.IsNullOrWhiteSpace(description) ? "Homeji Payment" : description.Trim();
        return trimmed.Length <= MaxPayPalItemDescriptionLength
            ? trimmed
            : trimmed[..MaxPayPalItemDescriptionLength];
    }

    private static string BuildQuoteMessage(PayPalCurrencyQuoteDto quote) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Thanh toán quốc tế {quote.TotalUsd:0.00} USD (Tỷ giá: 1 USD = {quote.ExchangeRateVnd:0} VND · Phí NH & cổng: {quote.TotalFeeUsd:0.00} USD)");

    private static string BuildPaidMessage(PayPalCurrencyQuoteDto quote, string? wallet)
    {
        var channel = wallet switch
        {
            "google-pay" => "Google Pay",
            "apple-pay" => "Apple Pay",
            _ => "PayPal",
        };
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Đã thanh toán {quote.TotalUsd:0.00} USD qua {channel} (Tỷ giá: 1 USD = {quote.ExchangeRateVnd:0} VND)");
    }

    private static string FormatUsd(decimal usd) => usd.ToString("0.00", CultureInfo.InvariantCulture);

    private static string SerializeStoredPayload(StoredPayPalPayload payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    private static PayPalCurrencyQuoteDto? TryReadStoredQuote(string? rawProviderPayload)
    {
        if (string.IsNullOrWhiteSpace(rawProviderPayload))
        {
            return null;
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredPayPalPayload>(rawProviderPayload, JsonOptions);
            if (stored?.Quote is { TotalUsd: > 0, AmountVnd: > 0 })
            {
                return stored.Quote;
            }

            var direct = JsonSerializer.Deserialize<PayPalCurrencyQuoteDto>(rawProviderPayload, JsonOptions);
            return direct is { TotalUsd: > 0, AmountVnd: > 0 } ? direct : null;
        }
        catch (JsonException)
        {
            return null;
        }
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
            ? "Thanh toán Homeji"
            : description.Trim();

        return normalized.Length <= PaymentTransaction.MaxDescriptionLength
            ? normalized
            : normalized[..PaymentTransaction.MaxDescriptionLength];
    }

    private static bool IsOrderId(string? id) =>
        id is { Length: > 0 and <= 32 } && id.All(char.IsAsciiLetterOrDigit);

    private static ExternalDependencyException ProviderError() =>
        new("Cổng PayPal chưa trả về kết quả hợp lệ. Vui lòng kiểm tra cấu hình hoặc thử lại.");

    private static RequestValidationException Validation(string field, string message) =>
        new(new Dictionary<string, string[]> { [field] = [message] });

    private sealed record StoredPayPalPayload(PayPalCurrencyQuoteDto Quote, string? CaptureJson = null);
    private sealed record OAuthToken([property: JsonPropertyName("access_token")] string? AccessToken);
    private sealed record Order(
        string? Id,
        string? Status,
        Link[]? Links,
        [property: JsonPropertyName("purchase_units")] PurchaseUnit[]? PurchaseUnits,
        [property: JsonPropertyName("payment_source")] PaymentSource? PaymentSource,
        [property: JsonIgnore] string? RawJson = null);
    private sealed record PaymentSource(
        [property: JsonPropertyName("apple_pay")] object? ApplePay,
        [property: JsonPropertyName("google_pay")] GooglePay? GooglePay);
    private sealed record GooglePay(Card? Card);
    private sealed record Card([property: JsonPropertyName("authentication_result")] AuthenticationResult? AuthenticationResult);
    private sealed record AuthenticationResult(
        [property: JsonPropertyName("liability_shift")] string? LiabilityShift,
        [property: JsonPropertyName("three_d_secure")] ThreeDSecure? ThreeDSecure);
    private sealed record ThreeDSecure(
        [property: JsonPropertyName("enrollment_status")] string? EnrollmentStatus,
        [property: JsonPropertyName("authentication_status")] string? AuthenticationStatus);
    private sealed record Link(string? Rel, string? Href);
    private sealed record PurchaseUnit(Payments? Payments);
    private sealed record Payments(Capture[]? Captures);
    private sealed record Capture(string? Id, string? Status, Money? Amount);
    private sealed record Money([property: JsonPropertyName("currency_code")] string? Currency, string? Value);
}
