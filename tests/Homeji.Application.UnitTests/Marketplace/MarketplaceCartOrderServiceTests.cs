using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Marketplace;
using Homeji.Application.Services.MarketplaceOrders;
using Homeji.Application.Services.MarketplaceOrders.Validation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplaceCartOrderServiceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CreateCartAsync_TwoItemsFromSameSeller_ReservesAndChargesInOneSave()
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var posts = new[]
        {
            CreateFoodPost(sellerId, "Bánh mì", 20_000),
            CreateFoodPost(sellerId, "Trà đào", 15_000),
        };
        var buyerWallet = ActivatedWallet(buyerId, 200_000);
        var sellerWallet = ActivatedWallet(sellerId, 100_000);
        sellerWallet.DebitWithdrawal(80_000, UtcNow.AddMinutes(-1));
        var orderRepository = new StubOrderRepository();
        var walletRepository = new StubWalletRepository(buyerWallet, sellerWallet);
        var notifications = new StubNotificationRepository();
        var publisher = new StubNotificationPublisher();
        var timeProvider = new StubTimeProvider();
        var service = new MarketplaceOrderService(
            new UserContext(new StubCurrentUser(buyerId), profiles: null!),
            orderRepository,
            new StubPostRepository(posts),
            notifications,
            publisher,
            timeProvider,
            walletRepository,
            new StubSellerPlanService(),
            new CreateMarketplaceOrderDtoValidator(timeProvider),
            new CreateMarketplaceCartOrderDtoValidator(timeProvider),
            Options.Create(new MarketplaceFinanceOptions
            {
                MinimumFoodOrder = 25_000,
                SellerReserve = 20_000,
            }),
            profiles: null!);

        var result = await service.CreateCartAsync(new CreateMarketplaceCartOrderDto(
            posts.Select(post => new MarketplaceCartItemDto(post.Id, 1)).ToArray(),
            UtcNow.AddHours(1),
            "Nhận tại bếp",
            null));

        Assert.Equal(2, result.Count);
        Assert.Equal(165_000, buyerWallet.Balance);
        Assert.All(posts, post => Assert.Equal(1, post.ReservedQuantity));
        Assert.Equal(2, walletRepository.AddedTransactions.Count);
        Assert.Equal(2, orderRepository.Added.Count);
        Assert.Equal(2, notifications.Added.Count);
        Assert.Equal(2, publisher.Published.Count);
        Assert.Equal(1, orderRepository.SaveCount);
    }

    private static MarketplacePost CreateFoodPost(Guid sellerId, string title, decimal price) =>
        new(
            sellerId,
            title,
            $"Món {title}",
            price,
            "Mới làm trong ngày",
            "Đồ ăn",
            "Bếp Homeji",
            10.85m,
            106.77m,
            null,
            [$"https://cdn.example.com/{Guid.NewGuid():N}.jpg"],
            UtcNow.AddHours(-1),
            MarketplaceListingType.Food,
            10,
            "phần",
            15);

    private static WalletAccount ActivatedWallet(Guid userId, decimal amount)
    {
        var wallet = WalletAccount.Create(userId, UtcNow.AddHours(-2));
        wallet.CreditTopUp(amount, UtcNow.AddHours(-1));
        return wallet;
    }

    private sealed record StubCurrentUser(Guid? UserId) : ICurrentUser;

    private sealed class StubTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class StubPostRepository(IReadOnlyList<MarketplacePost> posts) : IMarketplacePostRepository
    {
        public Task<MarketplacePost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(posts.SingleOrDefault(post => post.Id == id));

        public Task<IReadOnlyList<MarketplacePost>> GetByIdsForUpdateAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarketplacePost>>(posts.Where(post => ids.Contains(post.Id)).ToArray());

        public Task<IReadOnlyList<MarketplacePost>> SearchActiveAsync(
            string? keyword, string? category, decimal? minPrice, decimal? maxPrice,
            decimal? minLatitude, decimal? maxLatitude, decimal? minLongitude, decimal? maxLongitude,
            int skip, int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(MarketplacePost post, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubOrderRepository : IMarketplaceOrderRepository
    {
        public List<MarketplaceOrder> Added { get; } = [];
        public int SaveCount { get; private set; }

        public Task<MarketplaceOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<MarketplaceOrder?>(null);

        public Task<bool> HasActiveAsync(Guid postId, Guid buyerId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<IReadOnlySet<Guid>> GetActivePostIdsAsync(
            IReadOnlyCollection<Guid> postIds,
            Guid buyerId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

        public Task<IReadOnlyList<MarketplaceOrder>> GetExpiredRequestedAsync(
            DateTimeOffset cutoff, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarketplaceOrder>>([]);

        public Task<IReadOnlyList<MarketplaceOrder>> GetForUserAsync(
            Guid userId, DateTimeOffset requestedCutoff, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MarketplaceOrder>>([]);

        public Task AddAsync(MarketplaceOrder order, CancellationToken cancellationToken = default)
        {
            Added.Add(order);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount += 1;
            return Task.CompletedTask;
        }
    }

    private sealed class StubWalletRepository(params WalletAccount[] wallets) : IWalletRepository
    {
        public List<WalletTransaction> AddedTransactions { get; } = [];

        public Task<WalletAccount?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(wallets.SingleOrDefault(wallet => wallet.UserId == userId));

        public Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(
            Guid userId, int take, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WalletTransaction>>([]);

        public Task<bool> HasTransactionAsync(
            Guid userId, WalletTransactionKind kind, Guid referenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task AddAccountAsync(WalletAccount account, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddTransactionAsync(WalletTransaction transaction, CancellationToken cancellationToken = default)
        {
            AddedTransactions.Add(transaction);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubSellerPlanService : IMarketplaceSellerPlanService
    {
        public Task<decimal> ResolveCommissionRateAsync(
            Guid sellerId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            Task.FromResult(0.10m);

        public Task<IReadOnlyList<MarketplaceSellerPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MarketplaceSellerSubscriptionDto> GetMineAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MarketplaceSellerSubscriptionDto> PurchaseAsync(
            string packageCode, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class StubNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];

        public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<Notification>> GetForUserAsync(
            Guid userId, bool unreadOnly, CancellationToken cancellationToken = default) =>
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
            Guid userId, Guid conversationId, DateTimeOffset readAt, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
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
