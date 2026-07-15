using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Marketplace;

public interface IMarketplaceSellerSubscriptionRepository
{
    Task<MarketplaceSellerSubscription?> GetActiveAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task AddAsync(MarketplaceSellerSubscription subscription, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
