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
        Assert.Equal(0, posts.IndividualMediaCalls);
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

    [Theory]
    [InlineData(RentalPostStatus.Active)]
    [InlineData(RentalPostStatus.Archived)]
    [InlineData(RentalPostStatus.Rented)]
    public async Task Unsave_removes_only_current_users_preference_and_decrements_once(RentalPostStatus status)
    {
        var user = Guid.NewGuid(); var other = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        var post = ActivePost(now); post.ApplySaveDelta(2);
        if (status == RentalPostStatus.Archived) post.Archive(now);
        if (status == RentalPostStatus.Rented) post.MarkRented(now);
        var saves = new SavedRepository([new(user, post.Id, now), new(other, post.Id, now)]);
        var service = Service(user, saves, new PostRepository([post]));
        await service.UnsaveAsync(post.Id);
        await service.UnsaveAsync(post.Id);
        Assert.Equal(1, post.SaveCount);
        Assert.Equal(other, Assert.Single(saves.Remaining).UserId);
        Assert.Equal(1, saves.SaveChangesCalls);
    }

    [Fact]
    public async Task Unsave_cleans_up_saved_preference_when_post_is_missing()
    {
        var user = Guid.NewGuid(); var id = Guid.NewGuid();
        var saves = new SavedRepository([new(user, id, DateTimeOffset.UtcNow)]);
        await Service(user, saves, new PostRepository()).UnsaveAsync(id);
        Assert.Empty(saves.Remaining);
        Assert.Equal(1, saves.SaveChangesCalls);
    }

    [Fact]
    public async Task Unsave_does_not_change_another_users_preference_or_total()
    {
        var post = ActivePost(DateTimeOffset.UtcNow); post.ApplySaveDelta(1);
        var other = Guid.NewGuid();
        var saves = new SavedRepository([new(other, post.Id, DateTimeOffset.UtcNow)]);
        await Service(Guid.NewGuid(), saves, new PostRepository([post])).UnsaveAsync(post.Id);
        Assert.Equal(other, Assert.Single(saves.Remaining).UserId);
        Assert.Equal(1, post.SaveCount);
        Assert.Equal(0, saves.SaveChangesCalls);
    }

    [Fact]
    public async Task Save_still_rejects_an_inactive_post()
    {
        var user = Guid.NewGuid(); var post = ActivePost(DateTimeOffset.UtcNow); post.Archive(DateTimeOffset.UtcNow);
        var saves = new SavedRepository([]);
        await Assert.ThrowsAsync<Homeji.Application.Common.Exceptions.NotFoundException>(() =>
            Service(user, saves, new PostRepository([post])).SaveAsync(post.Id));
        Assert.Empty(saves.Remaining);
        Assert.Equal(0, saves.SaveChangesCalls);
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
        public List<SavedPost> Remaining { get; } = saved.ToList();
        public int SaveChangesCalls { get; private set; }
        public Task<IReadOnlyList<SavedPost>> GetByUserAsync(Guid user, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SavedPost>>(Remaining.Where(x => x.UserId == user).ToArray());
        public Task<bool> ExistsAsync(Guid user, Guid post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(SavedPost post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> RemoveAsync(Guid user, Guid post, CancellationToken cancellationToken = default) =>
            Task.FromResult(Remaining.RemoveAll(x => x.UserId == user && x.RentalPostId == post) > 0);
        public Task<IReadOnlyList<SavedPost>> GetByPostAsync(Guid post, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) { SaveChangesCalls++; return Task.CompletedTask; }
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
        public int IndividualMediaCalls { get; private set; }
        public IReadOnlyCollection<Guid>? RequestedIds { get; private set; }
        public CancellationToken ReceivedToken { get; private set; }
        public Task<IReadOnlyList<RentalPost>> SearchActiveAsync(
            RentalPostSearchDto search,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RentalPost>>([]);

        public Task<RentalPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult((posts ?? []).SingleOrDefault(x => x.Id == id));

        public Task<RentalPost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default)
        {
            IndividualMediaCalls++;
            return GetByIdAsync(id, cancellationToken);
        }

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
