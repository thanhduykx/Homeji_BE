using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.MarketplaceOrders;

public interface IMarketplaceOrderRepository
{
    Task<MarketplaceOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAsync(Guid postId, Guid buyerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketplaceOrder>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(MarketplaceOrder order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
