using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Marketplace;

public interface IMarketplacePostRepository
{
    Task<MarketplacePost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default);

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
