using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class UserSubscriptionRepository : IUserSubscriptionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserSubscriptionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserSubscription?> GetActivePremiumAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.UserId == userId
                && subscription.Tier == SubscriptionTier.Premium
                && subscription.Status == SubscriptionStatus.Active
                && subscription.StartedAt <= now
                && subscription.ExpiresAt > now)
            .OrderByDescending(subscription => subscription.ExpiresAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, UserSubscription>> GetActivePremiumByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, UserSubscription>();
        }

        var distinctUserIds = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToArray();

        if (distinctUserIds.Length == 0)
        {
            return new Dictionary<Guid, UserSubscription>();
        }

        var subscriptions = await _dbContext.UserSubscriptions
            .AsNoTracking()
            .Where(subscription =>
                distinctUserIds.Contains(subscription.UserId)
                && subscription.Tier == SubscriptionTier.Premium
                && subscription.Status == SubscriptionStatus.Active
                && subscription.StartedAt <= now
                && subscription.ExpiresAt > now)
            .OrderByDescending(subscription => subscription.ExpiresAt)
            .ToListAsync(cancellationToken);

        return subscriptions
            .GroupBy(subscription => subscription.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.First());
    }

    public Task<UserSubscription?> GetByPaymentTransactionIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserSubscriptions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                subscription => subscription.PaymentTransactionId == paymentTransactionId,
                cancellationToken);
    }

    public async Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default)
    {
        await _dbContext.UserSubscriptions.AddAsync(subscription, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
