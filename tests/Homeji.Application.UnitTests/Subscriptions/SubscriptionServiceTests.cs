using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.Subscriptions;

public sealed class SubscriptionServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Current_subscription_preserves_database_package_identity()
    {
        var userId = Guid.NewGuid();
        var subscription = UserSubscription.CreatePremium(userId, "PREMIUM_90",
            "Homeji Premium 90 ngày", Guid.NewGuid(), Now.AddDays(-83), 90, Now.AddDays(-83));
        var repository = new SubscriptionRepository(subscription);
        var result = await CreateService(userId, repository).GetMySubscriptionAsync();

        Assert.True(result.IsPremium);
        Assert.Equal(subscription.PackageCode, result.PackageCode);
        Assert.Equal(subscription.PackageName, result.PackageName);
        Assert.Equal(subscription.ExpiresAt, result.PremiumExpiresAt);
        Assert.Equal(userId, repository.RequestedUserId);
        Assert.Equal(Now, repository.RequestedTime);
    }

    [Fact]
    public async Task Failed_database_lookup_is_not_converted_to_free()
    {
        var repository = new SubscriptionRepository(null) { Fail = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateService(Guid.NewGuid(), repository).GetMySubscriptionAsync());
    }

    [Fact]
    public async Task Missing_active_subscription_returns_basic()
    {
        var result = await CreateService(Guid.NewGuid(), new SubscriptionRepository(null))
            .GetMySubscriptionAsync();
        Assert.False(result.IsPremium);
        Assert.Null(result.PackageCode);
        Assert.Null(result.PackageName);
    }

    [Fact]
    public async Task Anonymous_request_cannot_read_subscriptions()
    {
        var repository = new SubscriptionRepository(null);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            CreateService(null, repository).GetMySubscriptionAsync());
        Assert.Null(repository.RequestedUserId);
    }

    private static SubscriptionService CreateService(Guid? userId, SubscriptionRepository repository) =>
        new(new UserContext(new CurrentUser(userId), null!), repository, null!,
            Options.Create(new PremiumSubscriptionOptions()), new Clock());

    private sealed class CurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId => userId;
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class SubscriptionRepository(UserSubscription? subscription) : IUserSubscriptionRepository
    {
        public bool Fail { get; init; }
        public Guid? RequestedUserId { get; private set; }
        public DateTimeOffset? RequestedTime { get; private set; }
        public Task<UserSubscription?> GetActivePremiumAsync(Guid userId, DateTimeOffset now,
            CancellationToken cancellationToken = default)
        {
            RequestedUserId = userId;
            RequestedTime = now;
            if (Fail) throw new InvalidOperationException("Database unavailable");
            return Task.FromResult(subscription);
        }
        public Task<IReadOnlyDictionary<Guid, UserSubscription>> GetActivePremiumByUserIdsAsync(
            IReadOnlyCollection<Guid> ids, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<UserSubscription?> GetByPaymentTransactionIdAsync(Guid id,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(UserSubscription value, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
