using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.SavedPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.SavedPosts;

public sealed class SavedPostBatchLoadingTests
{
    [Fact]
    public async Task Saved_rooms_use_one_batch_preserve_saved_order_and_hide_inactive_or_missing_posts()
    {
        var user = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var first = ActivePost(now); var second = ActivePost(now);
        var hidden = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.VacantRoom, now);
        var missing = Guid.NewGuid();
        var saves = new SavedRepository([new(user, second.Id, now), new(user, hidden.Id, now.AddMinutes(-1)),
            new(user, first.Id, now.AddMinutes(-2)), new(user, missing, now.AddMinutes(-3))]);
        var posts = new PostRepository([first, hidden, second]);
        var service = Service(user, saves, posts);
        using var cancellation = new CancellationTokenSource();
        var result = await service.GetMineAsync(cancellation.Token);
        Assert.Equal(new[] { second.Id, first.Id }, result.Select(x => x.Id));
        Assert.Equal(1, posts.BatchCalls);
        Assert.Equal(4, posts.RequestedIds!.Count);
        Assert.Equal(cancellation.Token, posts.ReceivedToken);
        Assert.All(result, post => Assert.NotNull(post.ThumbnailPath));
    }

    [Fact]
    public async Task Empty_saved_list_does_not_query_rental_posts()
    {
        var posts = new PostRepository();
        Assert.Empty(await Service(Guid.NewGuid(), new SavedRepository([]), posts).GetMineAsync());
        Assert.Equal(0, posts.BatchCalls);
    }

    [Fact]
    public async Task Cancellation_is_forwarded_to_batch_repository()
    {
        var user = Guid.NewGuid(); var post = ActivePost(DateTimeOffset.UtcNow);
        var posts = new PostRepository([post]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Service(user,
            new SavedRepository([new(user, post.Id, DateTimeOffset.UtcNow)]), posts).GetMineAsync(cancellation.Token));
        Assert.Equal(1, posts.BatchCalls);
    }

    private static SavedPostService Service(Guid user, SavedRepository saves, PostRepository posts)
    {
        var profiles = new ProfileRepository();
        return new(new UserContext(new CurrentUser(user), profiles), saves, posts, profiles, TimeProvider.System);
    }
    private static RentalPost ActivePost(DateTimeOffset now)
    {
        var owner = Guid.NewGuid();
        var post = RentalPost.CreateDraft(owner, RentalPostType.VacantRoom, now);
        post.UpdateDetails(RentalPostType.VacantRoom, "Phòng đã lưu", "Thông tin phòng kiểm thử", 1_500_000, 0,
            25, "Linh Trung", 10.85m, 106.77m, [], now);
        for (var i = 0; i < 3; i++) post.AddMedia(MediaType.Image, "homeji-media", $"{owner}/image-{i}.jpg", i == 0, i, now);
        post.Submit(now); post.Approve(now); return post;
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class SavedRepository(IReadOnlyList<SavedPost> saved) : ISavedPostRepository
    {
        public Task<IReadOnlyList<SavedPost>> GetByUserAsync(Guid user, CancellationToken cancellationToken = default) => Task.FromResult(saved);
        public Task<bool> ExistsAsync(Guid user, Guid post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(SavedPost post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(Guid user, Guid post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SavedPost>> GetByPostAsync(Guid post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class ProfileRepository : IUserProfileRepository
    {
        public Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserProfile?>(UserProfile.Create(userId, "Người thuê", DateTimeOffset.UtcNow));

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

    private sealed class PostRepository(IReadOnlyList<RentalPost>? posts = null) : IRentalPostRepository
    {
        public int BatchCalls { get; private set; }
        public IReadOnlyCollection<Guid>? RequestedIds { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
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
            CancellationToken cancellationToken = default)
        {
            BatchCalls++; RequestedIds = ids; ReceivedToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<RentalPost>>((posts ?? []).Where(post => ids.Contains(post.Id)).ToArray());
        }

        public Task AddAsync(RentalPost post, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

}
