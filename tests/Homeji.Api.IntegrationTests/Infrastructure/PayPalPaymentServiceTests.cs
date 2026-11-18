using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IRepositories.Payments;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Emails;
using Homeji.Application.IServices.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class PayPalPaymentServiceTests
{
    private const string RedirectOrderId = "PAYPALORDER123456";
    private const string WalletOrderId = "WALLETORDER123456";

    [Fact]
    public async Task Exchange_rate_service_parses_vietcombank_xml_calculates_fees_and_caches()
    {
        var calls = 0;
        var handler = new DelegateHandler(request =>
        {
            calls++;
            Assert.Equal("portal.vietcombank.com.vn", request.RequestUri!.Host);
            const string xml = """
                <ExrateList>
                  <Exrate CurrencyCode="EUR" Sell="27,500.00" />
                  <Exrate CurrencyCode="USD" Transfer="25,400.00" Sell="25,450.00" />
                </ExrateList>
                """;
            return TextResponse(xml, "application/xml");
        });
        using var http = new HttpClient(handler);
        var clock = new FixedClock(DateTimeOffset.Parse("2026-11-18T02:00:00Z", CultureInfo.InvariantCulture));
        var options = Options.Create(new PayPalOptions
        {
            RateCacheMinutes = 30,
            BankFeePercent = 0.025m,
            GatewayFeePercent = 0.044m,
            GatewayFixedFeeUsd = 0.30m,
        });
        var service = new PayPalExchangeRateService(http, options, clock);

        var quote1 = await service.QuoteUsdAsync(200_000m);
        clock.Now = clock.Now.AddMinutes(10);
        var quote2 = await service.QuoteUsdAsync(200_000m);

        Assert.Equal(1, calls);
        Assert.Equal(25_450m, quote1.ExchangeRateVnd);
        Assert.Equal("Vietcombank (Tỷ giá bán USD)", quote1.RateSource);
        Assert.Equal(7.86m, quote1.BaseUsd);
        Assert.Equal(0.20m, quote1.BankFeeUsd);
        Assert.Equal(0.65m, quote1.GatewayFeeUsd);
        Assert.Equal(0.85m, quote1.TotalFeeUsd);
        Assert.Equal(8.71m, quote1.TotalUsd);
        Assert.Equal(quote1.TotalUsd, quote2.TotalUsd);
    }

    [Fact]
    public async Task Exchange_rate_service_falls_back_to_open_rate_then_configured_fallback()
    {
        var openRateHandler = new DelegateHandler(request =>
        {
            if (request.RequestUri!.Host == "portal.vietcombank.com.vn")
            {
                return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            }

            return JsonResponse("""{"result":"success","rates":{"VND":25300.45}}""");
        });
        using var openHttp = new HttpClient(openRateHandler);
        var clock = new FixedClock(DateTimeOffset.UtcNow);
        var options = Options.Create(new PayPalOptions { FallbackExchangeRateVnd = 25_500m });
        var openService = new PayPalExchangeRateService(openHttp, options, clock);

        var openQuote = await openService.QuoteUsdAsync(99_000m);
        Assert.Equal(25_300.45m, openQuote.ExchangeRateVnd);
        Assert.Equal("ExchangeRate-API (USD/VND quốc tế)", openQuote.RateSource);

        var offlineHandler = new DelegateHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var offlineHttp = new HttpClient(offlineHandler);
        var fallbackService = new PayPalExchangeRateService(offlineHttp, options, clock);

        var fallbackQuote = await fallbackService.QuoteUsdAsync(99_000m);
        Assert.Equal(25_500m, fallbackQuote.ExchangeRateVnd);
        Assert.Equal("Tỷ giá tham chiếu dự phòng", fallbackQuote.RateSource);
    }

    [Theory]
    [InlineData("Sandbox", "api-m.sandbox.paypal.com", "www.sandbox.paypal.com")]
    [InlineData("Live", "api-m.paypal.com", "www.paypal.com")]
    public async Task Create_and_capture_premium_payment_supports_sandbox_and_live_and_activates_subscription(
        string mode,
        string expectedApiHost,
        string expectedCheckoutHost)
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryStore(userId);
        var handler = new PayPalApiHandler(expectedApiHost, expectedCheckoutHost);
        using var http = new HttpClient(handler);
        var service = CreateService(http, store, userId, mode: mode);

        var created = await service.CreatePremiumPaymentAsync("PREMIUM_MONTHLY", 99_000m);
        Assert.Equal(99_000m, created.Amount);
        Assert.Equal(PaymentStatus.Pending, created.Status);
        Assert.Equal(RedirectOrderId, created.PayPalOrderId);
        Assert.Contains(expectedCheckoutHost, created.ApprovalUrl!, StringComparison.Ordinal);
        Assert.Equal(4.54m, created.Quote.TotalUsd);
        Assert.Equal(PaymentMethod.PayPal, store.Payments[0].Method);

        var captured = await service.CapturePaymentAsync(
            created.PaymentId,
            new CapturePayPalPaymentDto(PayPalOrderId: RedirectOrderId));

        Assert.Equal(PaymentStatus.Paid, captured.Status);
        Assert.Equal("CAPTURE-PP-1", captured.ExternalTransactionId);
        Assert.Single(store.Subscriptions);
        Assert.Equal("PREMIUM_MONTHLY", store.Subscriptions[0].PackageCode);
        Assert.Single(store.Receipts);

        // Idempotent second capture does not add a second subscription or receipt.
        var retry = await service.CapturePaymentAsync(
            created.PaymentId,
            new CapturePayPalPaymentDto(PayPalOrderId: RedirectOrderId));
        Assert.Equal(PaymentStatus.Paid, retry.Status);
        Assert.Single(store.Subscriptions);
        Assert.Single(store.Receipts);
    }

    [Fact]
    public async Task Wallet_topup_with_google_pay_and_apple_pay_credits_original_vnd_amount_and_enforces_3ds()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var store = new InMemoryStore(userId);
        var handler = new PayPalApiHandler("api-m.sandbox.paypal.com", "www.sandbox.paypal.com")
        {
            WalletOrderStatus = "APPROVED",
            WalletPaymentSource = """{"google_pay":{"card":{"authentication_result":{"liability_shift":"POSSIBLE"}}}}""",
        };
        using var http = new HttpClient(handler);
        var service = CreateService(http, store, userId, frontendOrigin: "https://homeji.onrender.com");
        var otherService = CreateService(http, store, otherUserId, frontendOrigin: "https://homeji.onrender.com");

        var created = await service.CreateWalletTopUpAsync(new CreatePayPalPaymentDto(200_000m, "Nạp ví Homeji"));
        var walletConfig = await service.GetPaymentWalletsAsync(created.PaymentId);
        Assert.True(walletConfig.Enabled);
        Assert.True(walletConfig.GooglePay);
        Assert.True(walletConfig.ApplePay);
        Assert.Equal(created.Quote.TotalUsd, walletConfig.Quote!.TotalUsd);

        var walletOrder = await service.CreatePaymentWalletOrderAsync(created.PaymentId, "google-pay");
        Assert.Equal(WalletOrderId, walletOrder.OrderId);
        Assert.Equal(created.Quote.TotalUsd.ToString("0.00", CultureInfo.InvariantCulture), walletOrder.TotalUsd);

        // Another user or tampered ticket cannot capture the payment.
        await Assert.ThrowsAsync<ForbiddenAccessException>(() =>
            otherService.CapturePaymentAsync(created.PaymentId, new CapturePayPalPaymentDto(Ticket: walletOrder.Ticket)));
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            service.CapturePaymentAsync(created.PaymentId, new CapturePayPalPaymentDto(Ticket: walletOrder.Ticket + "tamper")));

        var captured = await service.CapturePaymentAsync(
            created.PaymentId,
            new CapturePayPalPaymentDto(Ticket: walletOrder.Ticket));
        Assert.Equal(PaymentStatus.Paid, captured.Status);
        Assert.NotNull(store.Wallet);
        Assert.Equal(200_000m, store.Wallet!.Balance);
        Assert.Single(store.WalletTransactions);
        Assert.Single(store.Receipts);

        // Failed 3DS rejects capture and marks the payment failed.
        var failedCreated = await service.CreateWalletTopUpAsync(new CreatePayPalPaymentDto(150_000m, "Nạp ví lần 2"));
        handler.WalletPaymentSource = """{"google_pay":{"card":{"authentication_result":{"liability_shift":"NO","three_d_secure":{"enrollment_status":"Y","authentication_status":"N"}}}}}""";
        var failedOrder = await service.CreatePaymentWalletOrderAsync(failedCreated.PaymentId, "google-pay");
        var failedCapture = await service.CapturePaymentAsync(
            failedCreated.PaymentId,
            new CapturePayPalPaymentDto(Ticket: failedOrder.Ticket));
        Assert.Equal(PaymentStatus.Failed, failedCapture.Status);
        Assert.Equal(200_000m, store.Wallet.Balance);

        // Apple Pay succeeds when apple_pay payment source is returned.
        var appleCreated = await service.CreateWalletTopUpAsync(new CreatePayPalPaymentDto(100_000m, "Nạp ví Apple Pay"));
        handler.WalletPaymentSource = """{"apple_pay":{}}""";
        var appleOrder = await service.CreatePaymentWalletOrderAsync(appleCreated.PaymentId, "apple-pay");
        var appleCapture = await service.CapturePaymentAsync(
            appleCreated.PaymentId,
            new CapturePayPalPaymentDto(Ticket: appleOrder.Ticket));
        Assert.Equal(PaymentStatus.Paid, appleCapture.Status);
        Assert.Equal(300_000m, store.Wallet.Balance);
    }

    [Fact]
    public async Task Live_mode_rejects_sandbox_credentials_without_falling_back_to_sandbox()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryStore(userId);
        var tokenHostsTried = new List<string>();
        var handler = new DelegateHandler(request =>
        {
            Assert.Equal("/v1/oauth2/token", request.RequestUri!.AbsolutePath);
            tokenHostsTried.Add(request.RequestUri.Host);
            if (request.RequestUri.Host == "api-m.paypal.com")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent(
                        """{"error":"invalid_client","error_description":"Client Authentication failed"}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            return JsonResponse("""{"access_token":"sandbox-token"}""");
        });
        using var http = new HttpClient(handler);
        var service = CreateService(http, store, userId, mode: "Live", clientId: $"sb-client-{Guid.NewGuid():N}");

        var ex = await Assert.ThrowsAsync<ExternalDependencyException>(() =>
            service.CreateWalletTopUpAsync(new CreatePayPalPaymentDto(200_000m, "Nạp ví Homeji")));

        Assert.Equal(["api-m.paypal.com", "api-m.sandbox.paypal.com"], tokenHostsTried);
        Assert.Contains("PayPal Sandbox", ex.Message, StringComparison.Ordinal);
        Assert.Equal(PaymentStatus.Failed, store.Payments[0].Status);
    }

    [Fact]
    public async Task Create_premium_payment_wraps_transaction_in_retrying_execution_strategy()
    {
        var userId = Guid.NewGuid();
        var store = new InMemoryStore(userId);
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=59999;Database=homeji_missing;Username=homeji;Password=homeji",
                npgsql => npgsql.EnableRetryOnFailure(1, TimeSpan.FromMilliseconds(1), null))
            .Options;
        await using var dbContext = new ApplicationDbContext(dbOptions);
        using var http = new HttpClient(new PayPalApiHandler("api-m.paypal.com", "www.paypal.com"));
        var service = CreateService(http, store, userId, mode: "Live", dbContext: dbContext);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() =>
            service.CreatePremiumPaymentAsync("PREMIUM_MONTHLY", 99_000m));

        Assert.IsNotType<InvalidOperationException>(ex);
        Assert.DoesNotContain("does not support user-initiated transactions", ex.Message, StringComparison.Ordinal);
        Assert.Equal("RetryLimitExceededException", ex.GetType().Name);
    }

    private static PayPalPaymentService CreateService(
        HttpClient http,
        InMemoryStore store,
        Guid userId,
        string mode = "Sandbox",
        string frontendOrigin = "http://localhost:5173",
        string clientId = "fixture-client-id",
        ApplicationDbContext? dbContext = null)
    {
        var clock = new FixedClock(DateTimeOffset.Parse("2026-11-18T02:00:00Z", CultureInfo.InvariantCulture));
        var options = Options.Create(new PayPalOptions
        {
            Enabled = true,
            Mode = mode,
            ClientId = clientId,
            ClientSecret = "fixture-client-secret",
            GooglePayEnabled = true,
            ApplePayEnabled = true,
            FrontendOrigin = frontendOrigin,
            FallbackExchangeRateVnd = 25_000m,
            BankFeePercent = 0.025m,
            GatewayFeePercent = 0.044m,
            GatewayFixedFeeUsd = 0.30m,
        });
        var exchangeRateService = Stub<IPayPalExchangeRateService>.Create((method, args) =>
        {
            if (method.Name == nameof(IPayPalExchangeRateService.QuoteUsdAsync))
            {
                var vnd = (decimal)args![0]!;
                var baseUsd = Math.Round(vnd / 25_000m, 2, MidpointRounding.AwayFromZero);
                var bankFee = Math.Round(baseUsd * 0.025m, 2, MidpointRounding.AwayFromZero);
                var gatewayFee = Math.Round((baseUsd + bankFee) * 0.044m + 0.30m, 2, MidpointRounding.AwayFromZero);
                var totalFee = bankFee + gatewayFee;
                return Task.FromResult(new PayPalCurrencyQuoteDto(
                    vnd,
                    25_000m,
                    "Vietcombank (Tỷ giá bán USD)",
                    clock.Now,
                    baseUsd,
                    0.025m,
                    bankFee,
                    0.044m,
                    0.30m,
                    gatewayFee,
                    totalFee,
                    baseUsd + totalFee));
            }

            throw new InvalidOperationException();
        });

        var profile = UserProfile.Create(store.OwnerId, "PayPal User", clock.Now);
        var profiles = Stub<IUserProfileRepository>.Create((method, _) =>
            method.Name == nameof(IUserProfileRepository.GetByIdAsync)
                ? Task.FromResult<UserProfile?>(profile)
                : throw new InvalidOperationException());

        var paymentRepo = Stub<IPaymentTransactionRepository>.Create((method, args) => method.Name switch
        {
            nameof(IPaymentTransactionRepository.ExecuteForOrderAsync) => ((Delegate)args![1]!).DynamicInvoke(),
            nameof(IPaymentTransactionRepository.CancelOverdueAsync) => Task.FromResult(0),
            nameof(IPaymentTransactionRepository.GetByIdAsync) => Task.FromResult(store.Payments.SingleOrDefault(p => p.Id == (Guid)args![0]!)),
            nameof(IPaymentTransactionRepository.GetByOrderCodeAsync) => Task.FromResult(store.Payments.SingleOrDefault(p => p.OrderCode == (string)args![0]!)),
            nameof(IPaymentTransactionRepository.AddAsync) => AddPaymentAsync((PaymentTransaction)args![0]!),
            nameof(IPaymentTransactionRepository.SaveChangesAsync) => Task.CompletedTask,
            _ => throw new InvalidOperationException(method.Name),
        });

        var subscriptionRepo = Stub<IUserSubscriptionRepository>.Create((method, args) => method.Name switch
        {
            nameof(IUserSubscriptionRepository.GetByPaymentTransactionIdAsync) =>
                Task.FromResult(store.Subscriptions.SingleOrDefault(s => s.PaymentTransactionId == (Guid)args![0]!)),
            nameof(IUserSubscriptionRepository.GetPremiumTailAsync) =>
                Task.FromResult(store.Subscriptions.OrderByDescending(s => s.ExpiresAt).FirstOrDefault()),
            nameof(IUserSubscriptionRepository.AddAsync) => AddSubscriptionAsync((UserSubscription)args![0]!),
            _ => throw new InvalidOperationException(method.Name),
        });

        var walletRepo = Stub<IWalletRepository>.Create((method, args) => method.Name switch
        {
            nameof(IWalletRepository.GetAsync) => Task.FromResult(store.Wallet),
            nameof(IWalletRepository.HasTransactionAsync) =>
                Task.FromResult(store.WalletTransactions.Any(t => t.ReferenceId == (Guid)args![2]!)),
            nameof(IWalletRepository.AddAccountAsync) => SetWalletAsync((WalletAccount)args![0]!),
            nameof(IWalletRepository.AddTransactionAsync) => AddWalletTxAsync((WalletTransaction)args![0]!),
            _ => throw new InvalidOperationException(method.Name),
        });

        var receiptService = Stub<ITransactionReceiptService>.Create((method, args) => method.Name switch
        {
            nameof(ITransactionReceiptService.RecordPaymentAsync) => AddReceiptAsync((PaymentTransaction)args![0]!),
            _ => throw new InvalidOperationException(method.Name),
        });

        return new PayPalPaymentService(
            http,
            new UserContext(new CurrentUser(userId), profiles),
            paymentRepo,
            subscriptionRepo,
            walletRepo,
            receiptService,
            exchangeRateService,
            options,
            Options.Create(new PremiumSubscriptionOptions()),
            clock,
            dbContext: dbContext);

        Task AddPaymentAsync(PaymentTransaction payment)
        {
            store.Payments.Add(payment);
            return Task.CompletedTask;
        }

        Task AddSubscriptionAsync(UserSubscription subscription)
        {
            store.Subscriptions.Add(subscription);
            return Task.CompletedTask;
        }

        Task SetWalletAsync(WalletAccount wallet)
        {
            store.Wallet = wallet;
            return Task.CompletedTask;
        }

        Task AddWalletTxAsync(WalletTransaction tx)
        {
            store.WalletTransactions.Add(tx);
            return Task.CompletedTask;
        }

        Task AddReceiptAsync(PaymentTransaction payment)
        {
            if (!store.Receipts.Contains(payment.Id))
            {
                store.Receipts.Add(payment.Id);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryStore(Guid ownerId)
    {
        public Guid OwnerId { get; } = ownerId;
        public List<PaymentTransaction> Payments { get; } = [];
        public List<UserSubscription> Subscriptions { get; } = [];
        public WalletAccount? Wallet { get; set; }
        public List<WalletTransaction> WalletTransactions { get; } = [];
        public List<Guid> Receipts { get; } = [];
    }

    private sealed class CurrentUser(Guid id) : ICurrentUser
    {
        public Guid? UserId => id;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class DelegateHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(handler(request));
    }

    private sealed class PayPalApiHandler(string expectedApiHost, string expectedCheckoutHost) : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _orderAmounts = new(StringComparer.Ordinal);
        public string WalletOrderStatus { get; set; } = "APPROVED";
        public string WalletPaymentSource { get; set; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(expectedApiHost, request.RequestUri!.Host);
            if (request.RequestUri.AbsolutePath == "/v1/oauth2/token")
            {
                return JsonResponse("""{"access_token":"fixture-token"}""");
            }

            if (request.RequestUri.AbsolutePath == "/v2/checkout/orders")
            {
                using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var unit = body.RootElement.GetProperty("purchase_units")[0];
                var amount = unit.GetProperty("amount");
                Assert.Equal("USD", amount.GetProperty("currency_code").GetString());
                var usdValue = amount.GetProperty("value").GetString()!;

                if (body.RootElement.TryGetProperty("payment_source", out _))
                {
                    _orderAmounts[RedirectOrderId] = usdValue;
                    return JsonResponse($$"""
                        {"id":"{{RedirectOrderId}}","status":"PAYER_ACTION_REQUIRED","links":[{"rel":"payer-action","href":"https://{{expectedCheckoutHost}}/checkoutnow?token={{RedirectOrderId}}"}]}
                        """);
                }

                _orderAmounts[WalletOrderId] = usdValue;
                return JsonResponse($$"""{"id":"{{WalletOrderId}}","status":"CREATED"}""");
            }

            if (request.Method == HttpMethod.Get)
            {
                var id = request.RequestUri.AbsolutePath.Split('/')[^1];
                if (id == WalletOrderId)
                {
                    return JsonResponse($$"""{"id":"{{WalletOrderId}}","status":"{{WalletOrderStatus}}","payment_source":{{WalletPaymentSource}}}""");
                }

                return JsonResponse($$"""{"id":"{{RedirectOrderId}}","status":"APPROVED"}""");
            }

            var captureOrderId = request.RequestUri.AbsolutePath.Split('/')[^2];
            var capturedUsd = _orderAmounts[captureOrderId];
            return JsonResponse($$$"""
                {"id":"{{{captureOrderId}}}","status":"COMPLETED","purchase_units":[{"payments":{"captures":[{"id":"CAPTURE-PP-1","status":"COMPLETED","amount":{"currency_code":"USD","value":"{{{capturedUsd}}}"}}]}}]}
                """);
        }
    }

    private static HttpResponseMessage TextResponse(string content, string mediaType) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content, Encoding.UTF8, mediaType),
    };

    private static HttpResponseMessage JsonResponse(string json) => TextResponse(json, "application/json");
}
