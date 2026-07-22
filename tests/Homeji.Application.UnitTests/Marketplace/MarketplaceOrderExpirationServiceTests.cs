using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.Services.Marketplace;
using Homeji.Application.Services.MarketplaceOrders;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplaceOrderExpirationServiceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExpireOverdueAsync_RequestedForThirtyMinutes_RefundsAndHidesFinancialState()
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var post = CreateFoodPost(sellerId);
        var order = new MarketplaceOrder(
            post.Id,
            buyerId,
            sellerId,
            post.Price,
            UtcNow.AddHours(1),
            "Ký túc xá",
            null,
            UtcNow.AddMinutes(-31));
        post.Reserve(1, order.CreatedAt);

        var wallet = WalletAccount.Create(buyerId, UtcNow.AddHours(-1));
        wallet.CreditTopUp(200_000, UtcNow.AddHours(-1));
        wallet.DebitPurchase(order.AgreedPrice, order.CreatedAt);
        var wallets = new StubWalletRepository(wallet);
        var orders = new StubOrderRepository(order);
        var posts = new StubPostRepository(post);
        var notifications = new StubNotificationRepository();
        var publisher = new StubNotificationPublisher();
        var service = new MarketplaceOrderService(
            userContext: null!,
            orders,
            posts,
            notifications,
            publisher,
            new StubTimeProvider(),
            wallets,
            validator: null!,
            cartValidator: null!,
            Options.Create(new MarketplaceFinanceOptions { OrderRequestTimeoutMinutes = 30 }),
            profiles: null!);

        var expiredCount = await service.ExpireOverdueAsync();

        Assert.Equal(1, expiredCount);
        Assert.Equal(MarketplaceOrderStatus.Expired, order.Status);
        Assert.Equal(UtcNow, order.RefundedAt);
        Assert.Equal(200_000, wallet.Balance);
        Assert.Equal(0, wallet.TotalSpent);
        Assert.Equal(10, post.AvailableQuantity);
        Assert.Equal(0, post.ReservedQuantity);
        Assert.Single(wallets.AddedTransactions);
        Assert.Equal(WalletTransactionKind.Refund, wallets.AddedTransactions[0].Kind);
        Assert.Equal(2, notifications.Added.Count);
        Assert.Equal(2, publisher.Published.Count);
        Assert.Equal(1, orders.SaveCount);
    }

    private static MarketplacePost CreateFoodPost(Guid sellerId) =>
        new(
            sellerId,
            "Cơm gà",
            "Cơm gà sinh viên",
            35_000,
            "Mới làm trong ngày",
            "Cơm nhà",
            "Thủ Đức",
            10.85m,
            106.77m,
            null,
            ["https://cdn.example.com/rice.jpg"],
            UtcNow.AddHours(-1),
            MarketplaceListingType.Food,
            10,
            "phần",
            15);

    private sealed class StubTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class StubOrderRepository(MarketplaceOrder order) : IMarketplaceOrderRepository
    {
        public int SaveCount { get; private set; }

        public Task<MarketplaceOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MarketplaceOrder?>(id == order.Id ? order : null);

        public Task<bool> HasActiveAsync(Guid postId, Guid buyerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlyList<MarketplaceOrder>> GetExpiredRequestedAsync(
            DateTimeOffset cutoff,
            int take,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<MarketplaceOrder> result =
                order.Status == MarketplaceOrderStatus.Requested && order.CreatedAt <= cutoff
                    ? [order]
                    : [];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<MarketplaceOrder>> GetForUserAsync(
            Guid userId,
            DateTimeOffset requestedCutoff,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarketplaceOrder>>([]);

        public Task AddAsync(MarketplaceOrder addedOrder, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount += 1;
            return Task.CompletedTask;
        }
    }

    private sealed class StubPostRepository(MarketplacePost post) : IMarketplacePostRepository
    {
        public Task<MarketplacePost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MarketplacePost?>(id == post.Id ? post : null);

        public Task<IReadOnlyList<MarketplacePost>> SearchActiveAsync(
            string? keyword,
            string? category,
            MarketplaceListingType? listingType,
            decimal? minPrice,
            decimal? maxPrice,
            decimal? minLatitude,
            decimal? maxLatitude,
            decimal? minLongitude,
            decimal? maxLongitude,
            int skip,
            int take,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(MarketplacePost addedPost, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubWalletRepository(WalletAccount wallet) : IWalletRepository
    {
        public List<WalletTransaction> AddedTransactions { get; } = [];

        public Task<WalletAccount?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<WalletAccount?>(userId == wallet.UserId ? wallet : null);

        public Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(
            Guid userId,
            int take,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> HasTransactionAsync(
            Guid userId,
            WalletTransactionKind kind,
            Guid referenceId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task AddAccountAsync(WalletAccount account, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddTransactionAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
        {
            AddedTransactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];

        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Notification>> GetForUserAsync(
            Guid userId,
            bool unreadOnly,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Added.Add(notification);
            return Task.CompletedTask;
        }

        public Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
        {
            Added.AddRange(notifications);
            return Task.CompletedTask;
        }

        public Task MarkDirectMessagesReadAsync(
            Guid userId,
            Guid conversationId,
            DateTimeOffset readAt,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubNotificationPublisher : INotificationRealtimePublisher
    {
        public List<Notification> Published { get; } = [];

        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }
    }
}
