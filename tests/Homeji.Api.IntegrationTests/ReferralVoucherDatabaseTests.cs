using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Exceptions;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
namespace Homeji.Api.IntegrationTests;

public sealed class ReferralVoucherDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Verified_pair_gets_eight_vouchers_once_and_budget_stops_at_ten_pairs()
    {
        var inviter = await Seed(); var secondInviter = await Seed(); var invitee = await Seed();
        string code; string otherCode;
        await using (var db = Open()) code = (await Service(db, inviter).CreateCodeAsync()).Code!;
        await using (var db = Open()) otherCode = (await Service(db, secondInviter).CreateCodeAsync()).Code!;
        await using (var db = Open()) await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, inviter).ClaimAsync(code));
        var unverified = await Seed(false);
        await using (var db = Open()) await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, unverified).ClaimAsync(code));
        async Task Claim(Guid id, string invitation) { await using var db = Open(); await Service(db, id).ClaimAsync(invitation); }
        await Task.WhenAll(Claim(invitee, code), Claim(invitee, code));
        await using (var db = Open()) {
            Assert.Equal(8, await db.ReferralVouchers.CountAsync());
            Assert.Equal(100000m, (await db.ReferralCampaigns.SingleAsync()).Reserved);
            await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, invitee).ClaimAsync(otherCode));
        }
        for (var i = 0; i < 4; i++) await Claim(await Seed(), code);
        var cappedInvitee = await Seed();
        await using (var db = Open()) await Assert.ThrowsAsync<RequestValidationException>(() => Service(db, cappedInvitee).ClaimAsync(code));
        for (var i = 0; i < 5; i++) await Claim(await Seed(), otherCode);
        var third = await Seed(); string thirdCode;
        await using (var db = Open()) thirdCode = (await Service(db, third).CreateCodeAsync()).Code!;
        var overBudgetInvitee = await Seed();
        await using (var db = Open()) await Assert.ThrowsAsync<DomainException>(() => Service(db, overBudgetInvitee).ClaimAsync(thirdCode));
        await using (var db = Open()) {
            Assert.Equal(10, await db.ReferralClaims.CountAsync());
            Assert.Equal(80, await db.ReferralVouchers.CountAsync());
            Assert.Equal(1000000m, (await db.ReferralCampaigns.SingleAsync()).Reserved);
        }
        // Use a granted VIP voucher atomically with a wallet-funded purchase.
        await using (var db = Open()) {
            var voucher = await db.ReferralVouchers.FirstAsync(v => v.OwnerId == invitee && v.Purpose == "vip");
            var context = Context(db, invitee); var referrals = Service(db, invitee);
            var payment = new PremiumWalletPaymentService(db, context, Options.Create(new PremiumSubscriptionOptions()), TimeProvider.System, new TransactionReceiptService(db, context), referrals);
            var request = new WalletPremiumPaymentDto(Guid.NewGuid(), 79200, voucher.Id);
            var result = await payment.PurchaseAsync("PREMIUM_MONTHLY", request);
            Assert.Equal(79200m, result.Amount);
            Assert.Equal(result.Id, (await payment.PurchaseAsync("PREMIUM_MONTHLY", request)).Id);
            Assert.Equal(120800m, (await db.WalletAccounts.SingleAsync(w => w.UserId == invitee)).Balance);
            await Assert.ThrowsAsync<DomainException>(() => referrals.QuoteAsync(voucher.Id, "vip", 99000));
        }
        var foodSeller = await Seed();
        await using (var db = Open()) {
            var now = DateTimeOffset.UtcNow;
            var post = new MarketplacePost(foodSeller,"Food voucher test","Local food",30000,"Mới","Cơm","Thủ Đức",10.85m,106.8m,null,["https://example.test/food.jpg"],now,Homeji.Domain.Enums.MarketplaceListingType.Food,10,"phần",15);
            db.MarketplacePosts.Add(post); await db.SaveChangesAsync();
            var foodVoucher = await db.ReferralVouchers.FirstAsync(v => v.OwnerId == invitee && v.Purpose == "food");
            var profiles = new UserProfileRepository(db); var context = Context(db, invitee); var clock = TimeProvider.System;
            var orders = new Homeji.Application.Services.MarketplaceOrders.MarketplaceOrderService(context,
                new MarketplaceOrderRepository(db),new MarketplacePostRepository(db),new NotificationRepository(db),new Publisher(),clock,new WalletRepository(db),
                new Homeji.Application.Services.MarketplaceOrders.Validation.CreateMarketplaceOrderDtoValidator(clock),new Homeji.Application.Services.MarketplaceOrders.Validation.CreateMarketplaceCartOrderDtoValidator(clock),
                Options.Create(new Homeji.Application.Services.Marketplace.MarketplaceFinanceOptions()),profiles,new TransactionReceiptService(db,context),Service(db,invitee));
            var created = await orders.CreateCartAsync(new Homeji.Application.DTOs.MarketplaceOrders.CreateMarketplaceCartOrderDto([new(post.Id,4)],now.AddHours(1),"Thủ Đức",null,VoucherId:foodVoucher.Id));
            var order = Assert.Single(created);
            Assert.Equal(6000m,order.DiscountAmount); Assert.Equal(114000m,order.BuyerPaidAmount); Assert.Equal(108000m,order.SellerNetAmount);
            Assert.Equal(6800m,(await db.WalletAccounts.SingleAsync(w=>w.UserId==invitee)).Balance);
            await orders.CancelAsync(order.Id);
            db.ChangeTracker.Clear();
            Assert.Equal(120800m,(await db.WalletAccounts.SingleAsync(w=>w.UserId==invitee)).Balance);
            Assert.Null((await db.ReferralVouchers.SingleAsync(v=>v.Id==foodVoucher.Id)).ReferenceId);
            Assert.Equal(114000m,(await db.TransactionReceipts.SingleAsync(r=>r.ReferenceId==order.Id && r.Event==Homeji.Domain.Enums.ReceiptEvent.PurchaseRefunded)).Amount);
        }
    }
    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Local disposable DB only");
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection, o => o.EnableRetryOnFailure()).Options);
    }
    private static async Task<Guid> Seed(bool verified = true)
    {
        await using var db = Open(); var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var email = id + "@example.test";
        DateTimeOffset? confirmed = verified ? now : null;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at,created_at) VALUES ({id},{email},{confirmed},{now})");
        db.UserProfiles.Add(UserProfile.Create(id, "Referral test", now));
        var wallet = WalletAccount.Create(id, now); wallet.CreditTopUp(200000, now); db.WalletAccounts.Add(wallet);
        await db.SaveChangesAsync(); return id;
    }
    private static UserContext Context(ApplicationDbContext db, Guid user) => new(new CurrentUser(user), new UserProfileRepository(db));
    private static ReferralService Service(ApplicationDbContext db, Guid user) => new(db, Context(db, user), TimeProvider.System);
    private sealed class Publisher : Homeji.Application.Abstractions.Notifications.INotificationRealtimePublisher
    { public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask; }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
}
