using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IServices.Emails;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class PremiumWalletPaymentDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Concurrent_retries_debit_once_activate_once_and_record_one_receipt()
    {
        var user = await Seed(200000);
        var request = new WalletPremiumPaymentDto(Guid.NewGuid(), 99000);
        await using var first = Open(); await using var second = Open();
        var results = await Task.WhenAll(Service(first, user).PurchaseAsync("PREMIUM_MONTHLY", request), Service(second, user).PurchaseAsync("PREMIUM_MONTHLY", request));
        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Equal(PaymentMethod.Wallet, results[0].Method);
        Assert.Equal(PaymentStatus.Paid, results[0].Status);
        await using var verify = Open();
        var wallet = await verify.WalletAccounts.SingleAsync(w => w.UserId == user);
        Assert.Equal(101000, wallet.Balance); Assert.Equal(99000, wallet.TotalSpent);
        Assert.Single(await verify.PaymentTransactions.Where(p => p.UserId == user).ToListAsync());
        Assert.Single(await verify.UserSubscriptions.Where(p => p.UserId == user).ToListAsync());
        var ledger = Assert.Single(await verify.WalletTransactions.Where(p => p.WalletUserId == user).ToListAsync());
        Assert.Equal(WalletTransactionKind.SubscriptionPurchase, ledger.Kind); Assert.Equal(-99000, ledger.Amount);
        var receipt = Assert.Single(await verify.TransactionReceipts.Where(p => p.UserId == user).ToListAsync());
        Assert.Equal(ReceiptEvent.PaymentSucceeded, receipt.Event);
        Assert.Single(await verify.ReceiptEmailDeliveries.Where(p => p.ReceiptId == receipt.Id).ToListAsync());
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(verify, user).PurchaseAsync("OTHER", request));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Concurrent_different_orders_cannot_overspend_wallet()
    {
        var user = await Seed(100000);
        async Task<bool> TryPurchase()
        {
            await using var db = Open();
            try { await Service(db, user).PurchaseAsync("PREMIUM_MONTHLY", new(Guid.NewGuid(), 99000)); return true; }
            catch (RequestValidationException) { return false; }
        }
        var outcomes = await Task.WhenAll(TryPurchase(), TryPurchase());
        Assert.Single(outcomes, paid => paid);
        await using var verify = Open();
        Assert.Equal(1000, (await verify.WalletAccounts.SingleAsync(w => w.UserId == user)).Balance);
        Assert.Single(await verify.PaymentTransactions.Where(p => p.UserId == user).ToListAsync());
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Price_change_and_insufficient_funds_leave_all_financial_state_unchanged()
    {
        var user = await Seed(50000);
        await using var db = Open();
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, user).PurchaseAsync("PREMIUM_MONTHLY", new(Guid.NewGuid(), 1)));
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, user).PurchaseAsync("PREMIUM_MONTHLY", new(Guid.NewGuid(), 99000)));
        Assert.Equal(50000, (await db.WalletAccounts.SingleAsync(w => w.UserId == user)).Balance);
        Assert.False(await db.PaymentTransactions.AnyAsync(p => p.UserId == user));
        Assert.False(await db.UserSubscriptions.AnyAsync(p => p.UserId == user));
        Assert.False(await db.TransactionReceipts.AnyAsync(p => p.UserId == user));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Receipt_failure_rolls_back_wallet_and_entitlement()
    {
        var user = await Seed(200000);
        await using (var db = Open())
            await Assert.ThrowsAsync<InvalidOperationException>(() => Service(db, user, new FailingReceipt()).PurchaseAsync("PREMIUM_MONTHLY", new(Guid.NewGuid(), 99000)));
        await using var verify = Open();
        Assert.Equal(200000, (await verify.WalletAccounts.SingleAsync(w => w.UserId == user)).Balance);
        Assert.False(await verify.PaymentTransactions.AnyAsync(p => p.UserId == user));
        Assert.False(await verify.UserSubscriptions.AnyAsync(p => p.UserId == user));
        Assert.False(await verify.WalletTransactions.AnyAsync(p => p.WalletUserId == user));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Renewal_starts_after_existing_premium_and_retry_survives_catalog_price_change()
    {
        var user = await Seed(300000);
        var expires = DateTimeOffset.UtcNow.AddDays(10);
        await using var db = Open();
        db.UserSubscriptions.Add(UserSubscription.CreatePremium(user, "OLD", "Existing", null, expires.AddDays(-30), 30, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        var request = new WalletPremiumPaymentDto(Guid.NewGuid(), 99000);
        var paid = await Service(db, user).PurchaseAsync("PREMIUM_MONTHLY", request);
        var added = await db.UserSubscriptions.SingleAsync(s => s.PaymentTransactionId == paid.Id);
        Assert.InRange((added.StartedAt - expires).TotalMilliseconds, -1, 1);
        var changedOptions = Options.Create(new PremiumSubscriptionOptions { Price = 120000 });
        var retry = new PremiumWalletPaymentService(db, Context(user), changedOptions, TimeProvider.System, new TransactionReceiptService(db, Context(user)));
        Assert.Equal(paid.Id, (await retry.PurchaseAsync("PREMIUM_MONTHLY", request)).Id);
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database required.");
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection, options => options.EnableRetryOnFailure()).Options);
    }
    private static async Task<Guid> Seed(decimal balance)
    {
        await using var db = Open(); var user = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({user})");
        db.UserProfiles.Add(UserProfile.Create(user, "Wallet test", DateTimeOffset.UtcNow));
        var wallet = WalletAccount.Create(user, DateTimeOffset.UtcNow); wallet.CreditTopUp(balance, DateTimeOffset.UtcNow);
        db.WalletAccounts.Add(wallet); await db.SaveChangesAsync(); return user;
    }
    private static UserContext Context(Guid user) => new(new CurrentUser(user), null!);
    private static PremiumWalletPaymentService Service(ApplicationDbContext db, Guid user, ITransactionReceiptService? receipts = null)
        => new(db, Context(user), Options.Create(new PremiumSubscriptionOptions()), TimeProvider.System, receipts ?? new TransactionReceiptService(db, Context(user)));
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class FailingReceipt : ITransactionReceiptService
    {
        public Task RecordPaymentAsync(PaymentTransaction payment, CancellationToken cancellationToken) => throw new InvalidOperationException("Injected receipt failure");
        public Task RecordWithdrawalAsync(WalletWithdrawalRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ReceiptPageDto> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<TransactionReceiptDto> GetMineByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string> GetMyHtmlAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
