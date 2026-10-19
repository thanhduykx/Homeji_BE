using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.DTOs.Wallets;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Application.Services.Wallets;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

// Audit-only regression probes. Run against a disposable local database.
[Collection("Payment integrity database")]
public sealed class PaymentAuditReproductionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T00:00:00Z", CultureInfo.InvariantCulture);
    private static readonly MomoOptions Momo = new() { PartnerCode = "audit", AccessKey = "audit", SecretKey = "audit-local-only", RedirectUrl = "https://example.test/return", IpnUrl = "https://example.test/ipn" };

    [LocalMarketplaceDatabaseFact]
    public async Task Concurrent_withdrawal_retries_resolve_same_id_and_reject_changed_payload()
    {
        var user = await Seed();
        var request = new CreateWalletWithdrawalDto(50000, "Audit bank", "123456789", "AUDIT USER", Guid.NewGuid());
        async Task<Guid> Submit()
        {
            await using var db = Open();
            return (await Withdrawals(db, user).CreateAsync(request)).Id;
        }
        var results = await Task.WhenAll(Submit(), Submit());
        Assert.Equal(results[0], results[1]);
        await using var verify = Open();
        Assert.Equal(450000, (await verify.WalletAccounts.SingleAsync(w => w.UserId == user)).Balance);
        Assert.Single(await verify.WalletTransactions.Where(t => t.WalletUserId == user).ToListAsync());
        await Assert.ThrowsAsync<Homeji.Application.Common.Exceptions.ConflictException>(() => Withdrawals(verify, user).CreateAsync(request with { Amount = 60000 }));
        await Assert.ThrowsAsync<Homeji.Application.Common.Exceptions.RequestValidationException>(() => Withdrawals(verify, user).CreateAsync(request with { RequestId = null }));
        var other = await Seed();
        await using var otherDb = Open();
        Assert.NotEqual(results[0], (await Withdrawals(otherDb, other).CreateAsync(request)).Id);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Gateway_timeout_keeps_intent_for_verified_late_callback()
    {
        var user = await Seed();
        PaymentTransaction? intent = null;
        using var http = new HttpClient(new CallbackProvider(async () =>
        {
            await using var check = Open();
            intent = await check.PaymentTransactions.SingleAsync(p => p.UserId == user);
            Assert.NotNull(intent.RequestId);
            throw new HttpRequestException("Injected lost response");
        }));
        await using (var db = Open())
            await Assert.ThrowsAsync<Homeji.Application.Common.Exceptions.ExternalDependencyException>(() => Service(db, user, http).CreatePremiumMomoPaymentAsync("PREMIUM_MONTHLY"));
        Assert.NotNull(intent);
        await using var callbackDb = Open();
        using var callbackHttp = new HttpClient();
        var service = Service(callbackDb, user, callbackHttp);
        Assert.Equal(PaymentStatus.Paid, (await service.HandleMomoIpnAsync(Success(intent, 100))).Status);
        await service.HandleMomoIpnAsync(Success(intent, 100));
        Assert.Single(await callbackDb.UserSubscriptions.Where(s => s.UserId == user).ToListAsync());
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Callback_during_provider_request_is_not_overwritten_by_link_response()
    {
        var user = await Seed();
        using var http = new HttpClient(new CallbackProvider(async () =>
        {
            await using var callbackDb = Open();
            var intent = await callbackDb.PaymentTransactions.SingleAsync(p => p.UserId == user);
            using var callbackHttp = new HttpClient();
            await Service(callbackDb, user, callbackHttp).HandleMomoIpnAsync(Success(intent, 101));
        }));
        await using var db = Open();
        var response = await Service(db, user, http).CreatePremiumMomoPaymentAsync("PREMIUM_MONTHLY");
        Assert.Equal(PaymentStatus.Paid, response.Status);
        Assert.Equal("https://example.test/pay", response.PayUrl);
        var saved = await db.PaymentTransactions.SingleAsync(p => p.UserId == user);
        Assert.Equal("101", saved.ExternalTransactionId);
        Assert.Contains("transId", saved.RawProviderPayload!, StringComparison.Ordinal);
        Assert.Single(await db.UserSubscriptions.Where(s => s.UserId == user).ToListAsync());
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Concurrent_gateway_renewals_append_to_one_shared_timeline()
    {
        var user = await Seed();
        var payments = Enumerable.Range(1, 3).Select(i =>
        {
            var p = new PaymentTransaction(user, PaymentMethod.Momo, 99000, $"audit-{Guid.NewGuid():N}", "Audit", Now, PaymentPurpose.PremiumSubscription, "PREMIUM_MONTHLY");
            p.AttachMomoPayment($"request-{i}", null, null, null, null, null, Now);
            return p;
        }).ToArray();
        await using (var seed = Open()) { seed.PaymentTransactions.AddRange(payments); await seed.SaveChangesAsync(); }
        await Task.WhenAll(payments.Select(async (payment, i) =>
        {
            await using var db = Open(); using var http = new HttpClient();
            await Service(db, user, http).HandleMomoIpnAsync(Success(payment, i + 1));
        }));
        await using var verify = Open();
        var rows = await verify.UserSubscriptions.Where(s => s.UserId == user).OrderBy(s => s.StartedAt).ToArrayAsync();
        Assert.Equal(3, rows.Length);
        Assert.Equal(rows[0].ExpiresAt, rows[1].StartedAt);
        Assert.Equal(rows[1].ExpiresAt, rows[2].StartedAt);
        Assert.Equal(90, (rows[^1].ExpiresAt - Now).TotalDays);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Three_paid_gateway_renewals_should_provide_ninety_days()
    {
        var user = await Seed();
        for (var i = 1; i <= 3; i++)
        {
            await using var db = Open();
            var payment = new PaymentTransaction(user, PaymentMethod.Momo, 99000, $"audit-{Guid.NewGuid():N}", "Audit", Now, PaymentPurpose.PremiumSubscription, "PREMIUM_MONTHLY");
            payment.AttachMomoPayment($"request-{i}", "https://example.test/pay", null, null, null, null, Now);
            db.PaymentTransactions.Add(payment);
            await db.SaveChangesAsync();
            using var http = new HttpClient();
            await Service(db, user, http).HandleMomoIpnAsync(Success(payment, i));
        }
        await using var verify = Open();
        var subscriptions = await verify.UserSubscriptions.Where(s => s.UserId == user).ToListAsync();
        Assert.Equal(3, subscriptions.Count);
        Assert.Equal(90, (subscriptions.Max(s => s.ExpiresAt) - Now).TotalDays);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Retrying_identical_withdrawal_should_not_debit_again()
    {
        var user = await Seed();
        var request = new CreateWalletWithdrawalDto(50000, "Audit bank", "123456789", "AUDIT USER", Guid.NewGuid());
        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = Open();
            var service = new WalletWithdrawalService(Context(user), new WalletRepository(db), new WalletWithdrawalRepository(db), new Clock(), new TransactionReceiptService(db, Context(user)));
            await service.CreateAsync(request);
        }
        await using var verify = Open();
        Assert.Equal(1, await verify.WalletWithdrawalRequests.CountAsync(w => w.UserId == user));
        Assert.Equal(450000, (await verify.WalletAccounts.SingleAsync(w => w.UserId == user)).Balance);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Two_momo_requests_in_same_millisecond_should_get_distinct_orders()
    {
        var user = await Seed();
        using var http = new HttpClient(new Provider());
        string first;
        await using (var db = Open()) first = (await Service(db, user, http).CreatePremiumMomoPaymentAsync("PREMIUM_MONTHLY")).OrderCode;
        await using var secondDb = Open();
        var second = await Service(secondDb, user, http).CreatePremiumMomoPaymentAsync("PREMIUM_MONTHLY");
        Assert.NotEqual(first, second.OrderCode);
    }

    private static MomoIpnDto Success(PaymentTransaction p, int id)
    {
        var r = new MomoIpnDto(Momo.PartnerCode, p.OrderCode, p.RequestId, 99000, "Audit", "momo_wallet", id, 0, "success", "qr", 1, "", null);
        var raw = $"accessKey={Momo.AccessKey}&amount={r.Amount}&extraData={r.ExtraData}&message={r.Message}&orderId={r.OrderId}&orderInfo={r.OrderInfo}&orderType={r.OrderType}&partnerCode={r.PartnerCode}&payType={r.PayType}&requestId={r.RequestId}&responseTime={r.ResponseTime}&resultCode={r.ResultCode}&transId={r.TransId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Momo.SecretKey));
        return r with { Signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant() };
    }
    private static PaymentService Service(ApplicationDbContext db, Guid user, HttpClient http) => new(http, Context(user), new PaymentTransactionRepository(db), new UserSubscriptionRepository(db), new WalletRepository(db), Options.Create(Momo), Options.Create(new PayOsOptions()), Options.Create(new PremiumSubscriptionOptions()), new Clock(), new TransactionReceiptService(db, Context(user)));
    private static UserContext Context(Guid user) => new(new CurrentUser(user), null!);
    private static WalletWithdrawalService Withdrawals(ApplicationDbContext db, Guid user) => new(Context(user), new WalletRepository(db), new WalletWithdrawalRepository(db), new Clock(), new TransactionReceiptService(db, Context(user)));
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Provider : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"resultCode\":0,\"payUrl\":\"https://example.test/pay\"}") });
    }
    private sealed class CallbackProvider(Func<Task> callback) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await callback();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"resultCode\":0,\"payUrl\":\"https://example.test/pay\"}") };
        }
    }
    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database required.");
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
    private static async Task<Guid> Seed()
    {
        await using var db = Open(); var user = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({user})");
        db.UserProfiles.Add(UserProfile.Create(user, "Audit", Now));
        var wallet = WalletAccount.Create(user, Now); wallet.CreditTopUp(500000, Now);
        db.WalletAccounts.Add(wallet); await db.SaveChangesAsync(); return user;
    }
}

[CollectionDefinition("Payment integrity database", DisableParallelization = true)]
public sealed class PaymentIntegrityTestGroup;

