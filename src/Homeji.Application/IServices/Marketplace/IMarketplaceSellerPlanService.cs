using Homeji.Application.DTOs.Marketplace;

namespace Homeji.Application.IServices.Marketplace;

public interface IMarketplaceSellerPlanService
{
    Task<IReadOnlyList<MarketplaceSellerPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<MarketplaceSellerSubscriptionDto> GetMineAsync(CancellationToken cancellationToken = default);
    Task<MarketplaceSellerSubscriptionDto> PurchaseAsync(string packageCode, CancellationToken cancellationToken = default);
    Task<decimal> ResolveCommissionRateAsync(Guid sellerId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
