using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.DTOs.Activities;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.Activities;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPostSearchTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(999)]
    public async Task SearchAsync_UndefinedListingType_IsRejected(int type)
    {
        var service = CreateComparisonService(null, []);
        var request = new RentalPostSearchDto(null, null, null, null, null, null, null, null, null,
            [], Type: (RentalPostType)type);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SearchAsync(request));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("other")]
    [InlineData("guest")]
    public async Task CompareAsync_UsesDetailVisibilityRules_WithoutWriting(string viewer)
    {
        var ownerId = Guid.NewGuid();
        var posts = new[] { CreateActiveTransfer(ownerId), CreateActiveTransfer(ownerId) };
        Guid? viewerId = viewer == "owner" ? ownerId : viewer == "other" ? Guid.NewGuid() : null;
        var service = CreateComparisonService(viewerId, posts);

        var result = await service.CompareAsync(new CompareRentalPostsDto(posts.Select(post => post.Id).ToArray()));

        Assert.Equal(posts.Select(post => post.Id), result.Posts.Select(item => item.Post.Id));
        foreach (var item in result.Posts)
        {
            if (viewer == "owner")
            {
                Assert.Equal("owner@example.com", item.Post.OwnerConsentContact);
                Assert.Equal("12 Đường A, Long Thạnh Mỹ, Thủ Đức", item.Post.Address);
                Assert.Equal(10.812345m, item.Post.Latitude);
                Assert.Equal(106.812345m, item.Post.Longitude);
            }
            else
            {
                Assert.Null(item.Post.OwnerConsentContact);
                Assert.Equal("Khu vực Long Thạnh Mỹ, Thủ Đức", item.Post.Address);
                Assert.Equal(10.812m, item.Post.Latitude);
                Assert.Equal(106.812m, item.Post.Longitude);
            }
        }
        Assert.All(posts, post => Assert.Equal(0, post.ViewCount));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompareAsync_RejectsHiddenOrDeletedSelections(bool deleted)
    {
        var ownerId = Guid.NewGuid();
        var first = CreateActiveTransfer(ownerId);
        var second = CreateActiveTransfer(ownerId);
        second.MarkRented(DateTimeOffset.UtcNow);
        var service = CreateComparisonService(Guid.NewGuid(), deleted ? [first] : [first, second]);
        await Assert.ThrowsAsync<NotFoundException>(() => service.CompareAsync(new CompareRentalPostsDto([first.Id, second.Id])));
    }

    private static RentalPostService CreateComparisonService(Guid? viewerId, IReadOnlyList<RentalPost> posts)
    {
        var profiles = new MissingProfileRepository();
        return new RentalPostService(new UserContext(new StubCurrentUser(viewerId), profiles),
            new EmptyRentalPostRepository(posts), new EmptySubscriptionRepository(), null!, null!, null!,
            new RejectingActivityService(), new EmptyReviewRepository(), profiles, null!, null!, TimeProvider.System);
    }

    private static RentalPost CreateActiveTransfer(Guid ownerId)
    {
        var now = DateTimeOffset.UtcNow;
        var post = RentalPost.CreateDraft(ownerId, RentalPostType.RoomTransfer, now);
        post.UpdateDetails(RentalPostType.RoomTransfer, "Pass phòng gần trường", "Phòng có hợp đồng còn hạn.",
            3_000_000, 3_000_000, 24, "12 Đường A, Long Thạnh Mỹ, Thủ Đức", 10.812345m, 106.812345m, [], now,
            availableFrom: new DateOnly(2026, 10, 10), transferKind: RoomTransferKind.LeaseAssignment,
            originalLeaseEndsOn: new DateOnly(2027, 10, 10), transferReason: "Chuyển nơi học.",
            ownerConsentConfirmed: true, ownerConsentContact: "owner@example.com");
        for (var index = 0; index < RentalPost.MinimumImageCountForSubmit; index++)
            post.AddMedia(MediaType.Image, "rental", $"image-{index}.jpg", index == 0, index, now);
        post.Submit(now);
        post.Approve(now, Guid.NewGuid(), "Chủ nhà đã xác nhận.");
        return post;
    }

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

    private sealed class EmptyRentalPostRepository(IReadOnlyList<RentalPost>? posts = null) : IRentalPostRepository
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
            Task.FromResult<IReadOnlyList<RentalPost>>((posts ?? []).Where(post => ids.Contains(post.Id)).ToArray());

        public Task AddAsync(RentalPost post, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class EmptyReviewRepository : IRentalReviewRepository
    {
        public Task<IReadOnlyList<RentalReview>> GetByPostIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<RentalReview>>([]);
        public Task<RentalReview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<RentalReview?> GetByPostAndReviewerAsync(Guid postId, Guid reviewerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RentalReview>> GetByPostAsync(Guid postId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(RentalReview review, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Remove(RentalReview review) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
