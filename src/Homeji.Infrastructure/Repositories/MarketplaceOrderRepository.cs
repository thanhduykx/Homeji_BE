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

    public Task<bool> HasActiveAsync(Guid postId, Guid buyerId, CancellationToken cancellationToken = default) =>
        _dbContext.MarketplaceOrders.AnyAsync(order =>
            order.MarketplacePostId == postId
            && order.BuyerId == buyerId
            && (order.Status == MarketplaceOrderStatus.Requested || order.Status == MarketplaceOrderStatus.Accepted),
            cancellationToken);

    public async Task<IReadOnlyList<MarketplaceOrder>> GetExpiredRequestedAsync(
        DateTimeOffset cutoff,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders
            .Where(order => order.Status == MarketplaceOrderStatus.Requested && order.CreatedAt <= cutoff)
            .OrderBy(order => order.CreatedAt)
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
