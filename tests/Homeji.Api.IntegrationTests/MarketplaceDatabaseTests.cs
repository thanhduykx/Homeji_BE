using Homeji.Application.IRepositories.Marketplace;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Homeji.Infrastructure.Migrations;
using Npgsql;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Marketplace;
using Homeji.Application.Services.MarketplaceOrders;
using Homeji.Application.Services.MarketplaceOrders.Validation;
using Homeji.Domain.Exceptions;
using Microsoft.Extensions.Options;
using Homeji.Application.Services.Conversations;

namespace Homeji.Api.IntegrationTests;

// These tests write only to an explicitly supplied, disposable loopback database.
public sealed class MarketplaceDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Goods_purchase_delivery_and_escrow_release_persist_without_duplicate_payout()
    {
        await using var db = await OpenAsync();
        var (post, buyer, seller, clock) = await SeedGoodsCheckoutAsync(db);
        var buyerService = OrderService(db, buyer, clock);
        var sellerService = OrderService(db, seller, clock);
        var order = await buyerService.CreateAsync(post.Id, new CreateMarketplaceOrderDto(clock.Now.AddHours(1), "Linh Trung", null));
        db.ChangeTracker.Clear();
        Assert.Equal(170_000, (await db.WalletAccounts.SingleAsync(wallet => wallet.UserId == buyer)).Balance);
        Assert.Equal(1, (await db.MarketplacePosts.SingleAsync(item => item.Id == post.Id)).ReservedQuantity);
        await sellerService.AcceptAsync(order.Id);
        db.ChangeTracker.Clear();
        await sellerService.MarkDeliveredAsync(order.Id);
        db.ChangeTracker.Clear();
        await buyerService.CompleteAsync(order.Id);
        db.ChangeTracker.Clear();
        Assert.Equal(100_000, (await db.WalletAccounts.SingleAsync(wallet => wallet.UserId == seller)).Balance);
        Assert.Null((await db.MarketplaceOrders.SingleAsync(item => item.Id == order.Id)).FundsReleasedAt);
        clock.Now = clock.Now.AddHours(25);
        Assert.Equal(1, await sellerService.ReleaseOverdueFundsAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(0, await sellerService.ReleaseOverdueFundsAsync());
        db.ChangeTracker.Clear();
        var saved = await db.MarketplaceOrders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(MarketplaceOrderStatus.Completed, saved.Status);
        Assert.NotNull(saved.FundsReleasedAt);
        Assert.Equal(127_000, (await db.WalletAccounts.SingleAsync(wallet => wallet.UserId == seller)).Balance);
        var inventory = await db.MarketplacePosts.SingleAsync(item => item.Id == post.Id);
        Assert.Equal(MarketplacePostStatus.Sold, inventory.Status);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(1, await db.WalletTransactions.CountAsync(item => item.ReferenceId == order.Id && item.Kind == WalletTransactionKind.SaleProceeds));
        // Completed/sold listings must still let the order's two participants contact each other.
        var sellerChat = await ConversationService(db, seller).StartMarketplaceOrderConversationAsync(order.Id);
        db.ChangeTracker.Clear();
        var buyerChat = await ConversationService(db, buyer).StartMarketplaceOrderConversationAsync(order.Id);
        Assert.Equal(sellerChat.Id, buyerChat.Id);
        Assert.Equal(buyer, sellerChat.OtherParticipantId);
        Assert.Equal(seller, buyerChat.OtherParticipantId);
        Assert.Equal(post.Id, sellerChat.SubjectId);
        await Assert.ThrowsAsync<Homeji.Application.Common.Exceptions.ForbiddenAccessException>(() =>
            ConversationService(db, Guid.NewGuid()).StartMarketplaceOrderConversationAsync(order.Id));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            ConversationService(db, Guid.Empty).StartMarketplaceOrderConversationAsync(order.Id));
        Assert.Equal(1, await db.PostConversations.CountAsync(item => item.SubjectId == post.Id));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Rejected_goods_order_refunds_once_and_restores_persisted_stock()
    {
        await using var db = await OpenAsync();
        var (post, buyer, seller, clock) = await SeedGoodsCheckoutAsync(db);
        var order = await OrderService(db, buyer, clock).CreateAsync(post.Id, new CreateMarketplaceOrderDto(clock.Now.AddHours(1), "Linh Trung", null));
        db.ChangeTracker.Clear();
        var sellerService = OrderService(db, seller, clock);
        await sellerService.RejectAsync(order.Id);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<DomainException>(() => sellerService.RejectAsync(order.Id));
        db.ChangeTracker.Clear();
        var saved = await db.MarketplaceOrders.SingleAsync(item => item.Id == order.Id);
        Assert.Equal(MarketplaceOrderStatus.Rejected, saved.Status);
        Assert.NotNull(saved.RefundedAt);
        Assert.Equal(200_000, (await db.WalletAccounts.SingleAsync(wallet => wallet.UserId == buyer)).Balance);
        var inventory = await db.MarketplacePosts.SingleAsync(item => item.Id == post.Id);
        Assert.Equal(1, inventory.AvailableQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(MarketplacePostStatus.Active, inventory.Status);
        Assert.Equal(1, await db.WalletTransactions.CountAsync(item => item.ReferenceId == order.Id && item.Kind == WalletTransactionKind.Refund));
    }

    private static async Task<(MarketplacePost Post, Guid Buyer, Guid Seller, CheckoutClock Clock)> SeedGoodsCheckoutAsync(ApplicationDbContext db)
    {
        var seller = await AddSellerAsync(db);
        var buyer = await AddSellerAsync(db);
        var clock = new CheckoutClock();
        var buyerWallet = WalletAccount.Create(buyer, clock.Now);
        buyerWallet.CreditTopUp(200_000, clock.Now);
        var sellerWallet = WalletAccount.Create(seller, clock.Now);
        sellerWallet.CreditTopUp(100_000, clock.Now);
        var post = new MarketplacePost(seller, "Bàn học kiểm thử", "Kiểm chứng giao dịch với database local", 30_000,
            "Còn tốt", "Bàn ghế", "Linh Trung", 10.85m, 106.77m, null, ["https://example.com/local-qa.jpg"], clock.Now);
        db.WalletAccounts.AddRange(buyerWallet, sellerWallet);
        db.MarketplacePosts.Add(post);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (post, buyer, seller, clock);
    }

    private static MarketplaceOrderService OrderService(ApplicationDbContext db, Guid user, CheckoutClock clock)
    {
        var profiles = new UserProfileRepository(db);
        return new MarketplaceOrderService(new UserContext(new CheckoutUser(user), profiles),
            new MarketplaceOrderRepository(db), new MarketplacePostRepository(db), new NotificationRepository(db),
            new LocalNotificationPublisher(), clock, new WalletRepository(db),
            new CreateMarketplaceOrderDtoValidator(clock), new CreateMarketplaceCartOrderDtoValidator(clock),
            Options.Create(new MarketplaceFinanceOptions()), profiles);
    }

    private static PostConversationService ConversationService(ApplicationDbContext db, Guid user)
    {
        var profiles = new UserProfileRepository(db);
        return new PostConversationService(new UserContext(new CheckoutUser(user), profiles),
            new PostConversationRepository(db), new RentalPostRepository(db), new MarketplacePostRepository(db),
            wantedPosts: null!, profiles, new NotificationRepository(db), new LocalNotificationPublisher(),
            TimeProvider.System, imageProcessor: null!, new MarketplaceOrderRepository(db));
    }

    private sealed class CheckoutUser(Guid user) : ICurrentUser { public Guid? UserId => user; }
    private sealed class CheckoutClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class LocalNotificationPublisher : INotificationRealtimePublisher
    {
        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Nearby_search_filters_circle_and_orders_before_paging()
    {
        await using var db = await OpenAsync();
        var seller = await AddSellerAsync(db);
        var now = DateTimeOffset.UtcNow;
        var near = Post(seller, 10.8501m, 106.77m, now.AddDays(-2));
        var far = Post(seller, 10.857m, 106.77m, now);
        var corner = Post(seller, 10.858m, 106.778m, now.AddMinutes(1));
        db.MarketplacePosts.AddRange(near, far, corner);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var repository = new MarketplacePostRepository(db);
        var search = new MarketplaceSearchQuery(null, null, MarketplaceListingType.Food, null, null,
            10.85m, 106.77m, 1, 10.84m, 10.86m, 106.76m, 106.78m, 0, 1, seller);

        var first = await repository.SearchActiveAsync(search);
        var second = await repository.SearchActiveAsync(search with { Skip = 1 });
        var all = await repository.SearchActiveAsync(search with { Take = 50 });

        Assert.Equal(near.Id, Assert.Single(first).Id);
        Assert.Equal(far.Id, Assert.Single(second).Id);
        Assert.DoesNotContain(all, post => post.Id == corner.Id);
        Assert.NotEmpty(first[0].Media);
        Assert.Empty(db.ChangeTracker.Entries());
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Concurrent_stock_reservations_do_not_oversell()
    {
        await using var first = await OpenAsync();
        var seller = await AddSellerAsync(first);
        var buyer = await AddSellerAsync(first);
        var wallet = WalletAccount.Create(buyer, DateTimeOffset.UtcNow);
        wallet.CreditTopUp(200_000, DateTimeOffset.UtcNow);
        first.WalletAccounts.Add(wallet);
        var post = Post(seller, 10.85m, 106.77m, DateTimeOffset.UtcNow, quantity: 1);
        first.MarketplacePosts.Add(post);
        await first.SaveChangesAsync();
        await using var second = await OpenAsync();
        var competing = await second.MarketplacePosts.SingleAsync(item => item.Id == post.Id);
        var competingWallet = await second.WalletAccounts.SingleAsync(item => item.UserId == buyer);
        post.Reserve(1, DateTimeOffset.UtcNow);
        competing.Reserve(1, DateTimeOffset.UtcNow);
        wallet.DebitPurchase(30_000, DateTimeOffset.UtcNow);
        competingWallet.DebitPurchase(30_000, DateTimeOffset.UtcNow);

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verification = await OpenAsync();
        var saved = await verification.MarketplacePosts.SingleAsync(item => item.Id == post.Id);
        Assert.Equal(0, saved.AvailableQuantity);
        Assert.Equal(1, saved.ReservedQuantity);
        Assert.Equal(170_000, (await verification.WalletAccounts.SingleAsync(item => item.UserId == buyer)).Balance);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Migration_backfill_preserves_legacy_cart_groups_without_zero_checkout_ids()
    {
        await using var db = await OpenAsync();
        await db.Database.OpenConnectionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TEMP TABLE legacy_checkout (
                "Id" uuid, "BuyerId" uuid, "SellerId" uuid, "CreatedAt" timestamptz, "CheckoutId" uuid);
            """);
        var buyer = Guid.NewGuid();
        var seller = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO legacy_checkout VALUES
            ({first}, {buyer}, {seller}, {now}, {Guid.Empty}),
            ({second}, {buyer}, {seller}, {now}, {Guid.Empty}),
            ({third}, {buyer}, {seller}, {now.AddSeconds(1)}, {Guid.Empty});
            """);
        var sql = new AddMarketplaceFulfillmentAndCheckout().UpOperations.OfType<SqlOperation>()
            .Single(operation => operation.Sql.Contains("WITH grouped", StringComparison.Ordinal)).Sql;
        await db.Database.ExecuteSqlRawAsync(sql.Replace("homeji.marketplace_orders", "pg_temp.legacy_checkout", StringComparison.Ordinal));
        var checkouts = await db.Database.SqlQueryRaw<Guid>("SELECT \"CheckoutId\" AS \"Value\" FROM legacy_checkout").ToListAsync();
        Assert.DoesNotContain(Guid.Empty, checkouts);
        Assert.Equal(2, checkouts.Distinct().Count());
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Distinct_checkouts_at_same_timestamp_remain_separate_and_delivery_roundtrips()
    {
        await using var db = await OpenAsync();
        var seller = await AddSellerAsync(db);
        var buyer = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var post = Post(seller, 10.85m, 106.77m, now);
        db.MarketplacePosts.Add(post);
        var first = new MarketplaceOrder(post.Id, buyer, seller, 30_000, now.AddHours(1), "Tiệm", null, now);
        var second = new MarketplaceOrder(post.Id, buyer, seller, 30_000, now.AddHours(1), "Tiệm", null, now,
            fulfillmentType: MarketplaceFulfillmentType.SellerDelivery,
            delivery: new MarketplaceDelivery("Người nhận thử nghiệm", "0901234567", "KTX Thủ Đức", 10.85m, 106.77m));
        db.MarketplaceOrders.AddRange(first, second);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var repository = new MarketplaceOrderRepository(db);
        var group = await repository.GetGroupByIdAsync(first.Id);
        var delivered = await repository.GetByIdAsync(second.Id);
        Assert.Equal(first.Id, Assert.Single(group).Id);
        Assert.Equal(MarketplaceFulfillmentType.SellerDelivery, delivered!.FulfillmentType);
        Assert.Equal("KTX Thủ Đức", delivered.DeliveryAddress);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Seed_reproducible_food_catalog_for_api_load_test()
    {
        await using var db = await OpenAsync();
        var seller = await AddSellerAsync(db);
        var now = DateTimeOffset.UtcNow;
        db.MarketplacePosts.AddRange(Enumerable.Range(0, 2000).Select(index =>
            Post(seller, 10.81m + (index % 100) * 0.0008m, 106.74m + (index / 100) * 0.003m, now.AddSeconds(-index))));
        await db.SaveChangesAsync();
        Assert.True(await db.MarketplacePosts.CountAsync() >= 2000);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Old_application_inserts_keep_cart_groups_during_rolling_deployment()
    {
        await using var db = await OpenAsync();
        var seller = await AddSellerAsync(db);
        var buyer = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var post = Post(seller, 10.85m, 106.77m, now);
        db.MarketplacePosts.Add(post);
        var first = new MarketplaceOrder(post.Id, buyer, seller, 30_000, now.AddHours(1), "Tiệm", null, now);
        var second = new MarketplaceOrder(post.Id, buyer, seller, 30_000, now.AddHours(1), "Tiệm", null, now);
        var other = new MarketplaceOrder(post.Id, buyer, seller, 30_000, now.AddHours(1), "Tiệm", null, now.AddSeconds(1));
        db.MarketplaceOrders.AddRange(first, second, other);
        foreach (var order in new[] { first, second, other })
            db.Entry(order).Property(item => item.CheckoutId).CurrentValue = Guid.Empty;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var saved = await db.MarketplaceOrders.Where(order => order.Id == first.Id || order.Id == second.Id || order.Id == other.Id).ToArrayAsync();
        Assert.DoesNotContain(saved, order => order.CheckoutId == Guid.Empty);
        Assert.Equal(saved.Single(order => order.Id == first.Id).CheckoutId, saved.Single(order => order.Id == second.Id).CheckoutId);
        Assert.NotEqual(saved.Single(order => order.Id == first.Id).CheckoutId, saved.Single(order => order.Id == other.Id).CheckoutId);
    }

    private static async Task<ApplicationDbContext> OpenAsync()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")
            ?? throw new InvalidOperationException("HOMEJI_TEST_DATABASE is required.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Tests require the disposable loopback homeji_quality database.");
        var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        return await Task.FromResult(db);
    }

    private static async Task<Guid> AddSellerAsync(ApplicationDbContext db)
    {
        var seller = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({seller})");
        db.UserProfiles.Add(UserProfile.Create(seller, "Tiệm thử nghiệm", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return seller;
    }

    private static MarketplacePost Post(Guid seller, decimal latitude, decimal longitude, DateTimeOffset now, int quantity = 10) =>
        new(seller, "Cơm sinh viên thử nghiệm", "Món ăn cho kiểm thử cục bộ", 30_000, "Mới", "Cơm",
            "Thủ Đức", latitude, longitude, null, ["https://example.com/test-food.jpg"], now,
            MarketplaceListingType.Food, quantity, "phần", 15);
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class LocalMarketplaceDatabaseFactAttribute : FactAttribute
{
    public LocalMarketplaceDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")))
            Skip = "Run scripts/quality/Test-LocalBackend.ps1 to use an isolated PostgreSQL instance.";
    }
}
