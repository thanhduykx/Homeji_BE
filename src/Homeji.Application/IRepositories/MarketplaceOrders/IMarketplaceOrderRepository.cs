using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.MarketplaceOrders;

public interface IMarketplaceOrderRepository
{
    Task<MarketplaceOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAsync(Guid postId, Guid buyerId, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetActivePostIdsAsync(
        IReadOnlyCollection<Guid> postIds,
        Guid buyerId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
    Task<IReadOnlyList<MarketplaceOrder>> GetExpiredRequestedAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MarketplaceOrder>> GetForUserAsync(
        Guid userId,
        DateTimeOffset requestedCutoff,
        CancellationToken cancellationToken = default);
    Task AddAsync(MarketplaceOrder order, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
