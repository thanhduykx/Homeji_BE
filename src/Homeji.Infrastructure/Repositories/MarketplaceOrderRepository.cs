using Homeji.Application.IRepositories.MarketplaceOrders;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class MarketplaceOrderRepository : IMarketplaceOrderRepository
{
    private readonly ApplicationDbContext _dbContext;
    public MarketplaceOrderRepository(ApplicationDbContext dbContext) => _dbContext = dbContext;

    public Task<MarketplaceOrder?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.MarketplaceOrders.SingleOrDefaultAsync(order => order.Id == id, cancellationToken);

    public async Task<IReadOnlyList<MarketplaceOrder>> GetGroupByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var anchor = await _dbContext.MarketplaceOrders
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
        if (anchor is null)
        {
            return [];
        }

        return await _dbContext.MarketplaceOrders
            .Where(order => order.BuyerId == anchor.BuyerId
                && order.SellerId == anchor.SellerId
                && order.CreatedAt == anchor.CreatedAt)
            .OrderBy(order => order.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveAsync(Guid postId, Guid buyerId, CancellationToken cancellationToken = default) =>
        _dbContext.MarketplaceOrders.AnyAsync(order =>
            order.MarketplacePostId == postId
            && order.BuyerId == buyerId
            && (order.Status == MarketplaceOrderStatus.Requested || order.Status == MarketplaceOrderStatus.Accepted),
            cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetActivePostIdsAsync(
        IReadOnlyCollection<Guid> postIds,
        Guid buyerId,
        CancellationToken cancellationToken = default) =>
        (await _dbContext.MarketplaceOrders.AsNoTracking()
            .Where(order => postIds.Contains(order.MarketplacePostId)
                && order.BuyerId == buyerId
                && (order.Status == MarketplaceOrderStatus.Requested
                    || order.Status == MarketplaceOrderStatus.Accepted))
            .Select(order => order.MarketplacePostId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<IReadOnlyList<MarketplaceOrder>> GetExpiredRequestedAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders
            .Where(order => order.Status == MarketplaceOrderStatus.Requested && order.CreatedAt <= cutoff)
            .OrderBy(order => order.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketplaceOrder>> GetFundsReleaseDueAsync(
        DateTimeOffset deliveredCutoff,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders
            .Where(order => order.FundsReleasedAt == null
                && order.DeliveredAt != null
                && order.DeliveredAt <= deliveredCutoff
                && (order.Status == MarketplaceOrderStatus.Delivered
                    || order.Status == MarketplaceOrderStatus.Completed))
            .OrderBy(order => order.DeliveredAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketplaceOrder>> GetForUserAsync(
        Guid userId,
        DateTimeOffset requestedCutoff,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders.AsNoTracking()
            .Where(order => order.Status != MarketplaceOrderStatus.Expired
                && !(order.Status == MarketplaceOrderStatus.Requested && order.CreatedAt <= requestedCutoff)
                && (order.BuyerId == userId || order.SellerId == userId))
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MarketplaceOrder order, CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders.AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _dbContext.SaveChangesAsync(cancellationToken);
}
