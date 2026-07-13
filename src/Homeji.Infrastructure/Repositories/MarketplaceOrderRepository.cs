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

    public async Task<IReadOnlyList<MarketplaceOrder>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders.AsNoTracking()
            .Where(order => order.BuyerId == userId || order.SellerId == userId)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MarketplaceOrder order, CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplaceOrders.AddAsync(order, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => _dbContext.SaveChangesAsync(cancellationToken);
}
