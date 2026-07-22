using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.DTOs.Activities;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.Activities;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPostSearchTests
{
    [Fact]
    public async Task SearchAsync_AuthenticatedUserWithoutProfile_DoesNotRecordActivity()
    {
        var userId = Guid.NewGuid();
        var profiles = new MissingProfileRepository();
        var activities = new RejectingActivityService();
        var service = new RentalPostService(
            new UserContext(new StubCurrentUser(userId), profiles),
            new EmptyRentalPostRepository(),
            new EmptySubscriptionRepository(),
            updateValidator: null!,
            mediaValidator: null!,
            moderation: null!,
            activities,
            reviews: null!,
            profiles,
            conversations: null!,
            appointments: null!,
            TimeProvider.System);

        var result = await service.SearchAsync(new RentalPostSearchDto(
            "Thủ Đức",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            [],
            1,
            20));

        Assert.Empty(result);
        Assert.Equal(0, activities.RecordCount);
    }

    private sealed record StubCurrentUser(Guid? UserId) : ICurrentUser;

    private sealed class RejectingActivityService : IUserActivityService
    {
        public int RecordCount { get; private set; }

        public Task RecordAsync(
            Guid userId,
            string action,
            string resourcePath,
            string httpMethod,
            int responseStatusCode,
            UserActivityType type = UserActivityType.General,
            Guid? relatedEntityId = null,
            string? details = null,
            CancellationToken cancellationToken = default)
        {
            RecordCount++;
            throw new InvalidOperationException("A user without a profile cannot own an activity row.");
        }

        public Task<IReadOnlyList<UserActivityDto>> GetMineAsync(
            UserActivityType? type,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserActivityDto>>([]);
    }

    private sealed class MissingProfileRepository : IUserProfileRepository
    {
        public Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(null);

        public Task<UserProfile> UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserProfile> SaveAsync(UserProfile profile, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserProfile>>([]);

        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);

        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(
            string address,
            decimal price,
            Guid excludedUserId,
            int take,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserProfile>>([]);
    }

    private sealed class EmptyRentalPostRepository : IRentalPostRepository
    {
        public Task<IReadOnlyList<RentalPost>> SearchActiveAsync(
            RentalPostSearchDto search,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RentalPost>>([]);

        public Task<RentalPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<RentalPost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RentalPost>> GetPendingAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RentalPost>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RentalPost>> GetByIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RentalPost>> GetByIdsWithMediaAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(RentalPost post, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EmptySubscriptionRepository : IUserSubscriptionRepository
    {
        public Task<IReadOnlyDictionary<Guid, UserSubscription>> GetActivePremiumByUserIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<Guid, UserSubscription>>(
                new Dictionary<Guid, UserSubscription>());

        public Task<UserSubscription?> GetActivePremiumAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserSubscription?> GetByPaymentTransactionIdAsync(
            Guid paymentTransactionId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
