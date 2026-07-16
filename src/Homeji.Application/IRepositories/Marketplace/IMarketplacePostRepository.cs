using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Marketplace;

public interface IMarketplacePostRepository
{
    Task<MarketplacePost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MarketplacePost?> GetSellerLocationAnchorAsync(
        Guid sellerId,
        Guid? excludingPostId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<MarketplacePost?>(null);

    Task<IReadOnlyList<MarketplacePost>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MarketplacePost>>([]);

    Task<IReadOnlyList<MarketplacePost>> GetByIdsForUpdateAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MarketplacePost>>([]);

    Task<IReadOnlyList<MarketplacePost>> SearchActiveAsync(
        string? keyword,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        decimal? minLatitude,
        decimal? maxLatitude,
        decimal? minLongitude,
        decimal? maxLongitude,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(MarketplacePost post, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
