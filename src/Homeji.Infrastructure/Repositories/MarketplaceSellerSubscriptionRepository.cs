using Homeji.Application.IRepositories.Marketplace;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class MarketplaceSellerSubscriptionRepository : IMarketplaceSellerSubscriptionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public MarketplaceSellerSubscriptionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MarketplaceSellerSubscription?> GetActiveAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default) =>
        _dbContext.MarketplaceSellerSubscriptions.AsNoTracking()
            .Where(subscription => subscription.UserId == userId
                && subscription.StartsAt <= now
                && subscription.ExpiresAt > now)
            .OrderByDescending(subscription => subscription.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task AddAsync(MarketplaceSellerSubscription subscription, CancellationToken cancellationToken = default) =>
        _dbContext.MarketplaceSellerSubscriptions.AddAsync(subscription, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
