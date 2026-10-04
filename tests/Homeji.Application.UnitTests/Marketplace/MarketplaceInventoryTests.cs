using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Marketplace;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplaceInventoryTests
{
    [Fact]
    public async Task Inventory_uses_authenticated_seller_and_keeps_sold_and_archived_items()
    {
        var sellerId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        MarketplacePost Create() => new(sellerId, "Bàn học", "Bàn gỗ", 100_000,
            "Còn tốt", "Nội thất", "Thủ Đức", 10.87m, 106.8m, null, ["https://example.test/table.jpg"], now);
        var active = Create();
        var sold = Create();
        sold.MarkSold(now);
        var archived = Create();
        archived.Archive(now);
        var repository = new Posts([active, sold, archived]);
        var service = CreateService(sellerId, repository);

        var result = await service.GetMineAsync();

        Assert.Equal(sellerId, repository.RequestedSeller);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, item => item.Status == MarketplacePostStatus.Sold);
        Assert.Contains(result, item => item.Status == MarketplacePostStatus.Archived);
        Assert.All(result, item => Assert.Equal(sellerId, item.SellerId));
    }

    [Fact]
    public async Task Anonymous_request_never_queries_inventory()
    {
        var repository = new Posts([]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(null, repository).GetMineAsync());
        Assert.Null(repository.RequestedSeller);
    }

    private static MarketplacePostService CreateService(Guid? userId, Posts posts) =>
        new(new UserContext(new CurrentUser(userId), null!), posts, null!, new Profiles(),
            null!, TimeProvider.System, null!, Options.Create(new MarketplaceFinanceOptions()));

    private sealed class CurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId => userId;
    }

    private sealed class Posts(IReadOnlyList<MarketplacePost> posts) : IMarketplacePostRepository
    {
        public Guid? RequestedSeller { get; private set; }
        public Task<IReadOnlyList<MarketplacePost>> GetBySellerAsync(Guid sellerId,
            CancellationToken cancellationToken = default)
        {
            RequestedSeller = sellerId;
            return Task.FromResult<IReadOnlyList<MarketplacePost>>(posts.Where(post => post.SellerId == sellerId).ToArray());
        }
        public Task<MarketplacePost?> GetByIdWithMediaAsync(Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MarketplacePost>> SearchActiveAsync(MarketplaceSearchQuery search,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(MarketplacePost post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Profiles : IUserProfileRepository
    {
        public Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult<UserProfile?>(null);
        public Task<UserProfile> UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserProfile> SaveAsync(UserProfile profile, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(string address, decimal price,
            Guid excludedUserId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
