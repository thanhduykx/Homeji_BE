using System.Reflection;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IServices.AI;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.Common.Exceptions;

namespace Homeji.Application.UnitTests.AI;

public sealed class RentalDecisionPriceBasisTests
{
    [Theory]
    [InlineData(false, 3, true)]
    [InlineData(false, 4, false)]
    [InlineData(true, 5, true)]
    [InlineData(true, 6, false)]
    public async Task Comparison_limit_comes_from_current_server_subscription(bool premium, int count, bool allowed)
    {
        var subscriptionRepository = Stub<IUserSubscriptionRepository>((method, args) =>
        {
            Assert.Equal(nameof(IUserSubscriptionRepository.GetActivePremiumAsync), method.Name);
            var now = (DateTimeOffset)args![1]!;
            return Task.FromResult(premium ? UserSubscription.CreatePremium((Guid)args[0]!, "TEST", "Premium", null, now, 30, now) : null);
        });
        var profiles = Stub<IUserProfileRepository>((method, _) => throw new InvalidOperationException(method.Name));
        var task = Service([], profiles, subscriptionRepository).CompareAsync(new(Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray()), CancellationToken.None);
        if (allowed) Assert.Equal(count, (await task).UnavailablePostIds.Count);
        else await Assert.ThrowsAsync<RequestValidationException>(() => task);
    }
    [Theory]
    [InlineData(UserRole.Renter, UserRole.Renter, "đồng/người/tháng", true)]
    [InlineData(UserRole.Landlord, UserRole.Landlord, "đồng/phòng/tháng", true)]
    [InlineData(UserRole.Renter, UserRole.Landlord, "Không thể so sánh", false)]
    [InlineData(UserRole.Renter, null, "Không thể so sánh", false)]
    [InlineData(UserRole.Admin, UserRole.Admin, "Không thể so sánh", false)]
    public async Task Compare_batches_owner_roles_and_only_compares_known_equal_price_bases(
        UserRole firstRole, UserRole? secondRole, string summaryFragment, bool comparable)
    {
        var first = Post(1_000_000); var second = Post(2_000_000);
        var profiles = new List<UserProfile> { Profile(first.OwnerId, firstRole) };
        if (secondRole.HasValue) profiles.Add(Profile(second.OwnerId, secondRole.Value));
        var batchCalls = 0;
        using var cancellation = new CancellationTokenSource();
        var profileRepository = Stub<IUserProfileRepository>((method, arguments) =>
        {
            Assert.Equal(nameof(IUserProfileRepository.GetByIdsAsync), method.Name);
            Assert.Equal(cancellation.Token, arguments![1]);
            Assert.Equal(new[] { first.OwnerId, second.OwnerId }.Order(), ((IReadOnlyCollection<Guid>)arguments[0]!).Order());
            batchCalls++;
            return Task.FromResult<IReadOnlyList<UserProfile>>(profiles);
        });
        var service = Service([first, second], profileRepository);
        var result = await service.CompareAsync(new([first.Id, second.Id], new()), cancellation.Token);
        Assert.Equal(1, batchCalls);
        Assert.Equal(firstRole, result.Posts.First().Post.OwnerRole);
        Assert.Equal(secondRole, result.Posts.Last().Post.OwnerRole);
        Assert.Contains(summaryFragment, result.Summary, StringComparison.Ordinal);
        if (comparable) Assert.Contains("1,000,000", result.Summary.Replace('.', ','), StringComparison.Ordinal);
        else Assert.DoesNotContain("Chênh lệch giá thuê giữa các tin", result.Summary, StringComparison.Ordinal);
        Assert.All(result.Posts, item => Assert.Equal(item.Post.Price, item.Post.Id == first.Id ? first.Price : second.Price));
    }

    [Fact]
    public async Task Missing_rent_blocks_numeric_difference_even_with_same_known_basis()
    {
        var first = Post(0); var second = Post(2_000_000);
        var profiles = Stub<IUserProfileRepository>((method, _) => method.Name == nameof(IUserProfileRepository.GetByIdsAsync)
            ? Task.FromResult<IReadOnlyList<UserProfile>>([Profile(first.OwnerId, UserRole.Landlord), Profile(second.OwnerId, UserRole.Landlord)])
            : throw new InvalidOperationException(method.Name));
        var response = await Service([first, second], profiles).CompareAsync(new([first.Id, second.Id]), CancellationToken.None);
        Assert.Contains("Không thể so sánh", response.Summary, StringComparison.Ordinal);
        Assert.Null(response.Posts.First().Cost.EstimatedMonthlyTotal);
    }

    [Fact]
    public async Task No_visible_posts_skips_owner_query_and_preserves_unavailable_ids()
    {
        var profiles = Stub<IUserProfileRepository>((method, _) => throw new InvalidOperationException(method.Name));
        var id = Guid.NewGuid();
        var response = await Service([], profiles).CompareAsync(new([id]), CancellationToken.None);
        Assert.Empty(response.Posts);
        Assert.Equal(id, Assert.Single(response.UnavailablePostIds));
    }

    private static RentalDecisionService Service(IReadOnlyList<RentalPost> posts, IUserProfileRepository profiles, IUserSubscriptionRepository? subscriptions = null)
    {
        var repository = Stub<IRentalPostRepository>((method, _) => method.Name == nameof(IRentalPostRepository.GetByIdsWithMediaAsync)
            ? Task.FromResult(posts) : throw new InvalidOperationException(method.Name));
        var commute = Stub<ICommuteClient>((method, _) => throw new InvalidOperationException(method.Name));
        var moderation = Stub<IAdminWorkQueueRepository>((method, _) => throw new InvalidOperationException(method.Name));
        return new(repository, new UserContext(new CurrentUser(Guid.NewGuid()), profiles), commute, moderation, TimeProvider.System, profiles, subscriptions!);
    }

    private static UserProfile Profile(Guid id, UserRole role)
    {
        var profile = UserProfile.Create(id, "Người đăng", DateTimeOffset.UtcNow);
        profile.SetRole(role, DateTimeOffset.UtcNow);
        return profile;
    }

    private static RentalPost Post(decimal price)
    {
        var now = DateTimeOffset.UtcNow;
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.RoommateShare, now);
        post.UpdateDetails(RentalPostType.RoommateShare, "Cần bạn ở ghép", "Thông tin kiểm thử", 1_000_000,
            1_000_000, 25, "Linh Trung", 10.85m, 106.80m, [], now, maxOccupants: 2, availableSlots: 1);
        for (var i = 0; i < 3; i++) post.AddMedia(MediaType.Image, "images", $"room/{i}.jpg", i == 0, i, now);
        post.Submit(now); post.Approve(now);
        typeof(RentalPost).GetProperty(nameof(RentalPost.Price))!.SetValue(post, price);
        return post;
    }

    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        var stub = DispatchProxy.Create<T, StrictInterfaceStub>();
        ((StrictInterfaceStub)(object)stub).Handler = invoke;
        return stub;
    }

    public class StrictInterfaceStub : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
