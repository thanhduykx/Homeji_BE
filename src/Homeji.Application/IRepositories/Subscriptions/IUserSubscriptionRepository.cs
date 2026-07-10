using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Subscriptions;

public interface IUserSubscriptionRepository
{
    Task<UserSubscription?> GetActivePremiumAsync(
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, UserSubscription>> GetActivePremiumByUserIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<UserSubscription?> GetByPaymentTransactionIdAsync(
        Guid paymentTransactionId,
        CancellationToken cancellationToken = default);

    Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
