using Homeji.Application.DTOs.Marketplace;

namespace Homeji.Application.IServices.Marketplace;

public interface IMarketplacePostService
{
    Task<IReadOnlyList<MarketplacePostDto>> SearchAsync(
        MarketplaceSearchDto request,
        CancellationToken cancellationToken = default);

    Task<MarketplacePostDto> GetDetailAsync(Guid id, CancellationToken cancellationToken = default);

    Task<MarketplacePostDto> CreateAsync(
        UpsertMarketplacePostDto request,
        CancellationToken cancellationToken = default);

    Task<MarketplacePostDto> UpdateAsync(
        Guid id,
        UpsertMarketplacePostDto request,
        CancellationToken cancellationToken = default);

    Task MarkSoldAsync(Guid id, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);
}
