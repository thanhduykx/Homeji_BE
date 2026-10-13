using System.Reflection;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IServices.Activities;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.Appointments;
using Homeji.Application.Services.Conversations;
using Homeji.Application.Services.Roommates;
using Homeji.Application.Services.SavedPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalTransferReadPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 17, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("guest", false)]
    [InlineData("renter", false)]
    [InlineData("owner", true)]
    [InlineData("admin", true)]
    public async Task Expired_detail_is_hidden_from_public_but_owner_and_admin_can_still_inspect_inventory(string viewerKind, bool allowed)
    {
        var post = Post(new DateOnly(2026, 10, 12));
        Guid? viewer = viewerKind == "guest" ? null : viewerKind == "owner" ? post.OwnerId : Guid.NewGuid();
        var profiles = Profiles(post.OwnerId, viewer, viewerKind == "admin" ? UserRole.Admin : UserRole.Renter);
        var writes = 0;
        var posts = Stub<IRentalPostRepository>((method, _) => method.Name switch
        {
            nameof(IRentalPostRepository.GetByIdWithMediaAsync) => Task.FromResult<RentalPost?>(post),
            nameof(IRentalPostRepository.SaveChangesAsync) => Save(),
            _ => throw new InvalidOperationException(method.Name),
        });
        Task Save() { writes++; return Task.CompletedTask; }
        var subscriptions = Stub<IUserSubscriptionRepository>((_, _) => Task.FromResult<IReadOnlyDictionary<Guid, UserSubscription>>(new Dictionary<Guid, UserSubscription>()));
        var activities = Stub<IUserActivityService>((_, _) => Task.CompletedTask);
        var service = new RentalPostService(new UserContext(new CurrentUser(viewer), profiles), posts, subscriptions,
            null!, null!, null!, activities, null!, profiles, null!, null!, new Clock());
        if (allowed) Assert.Equal(post.Id, (await service.GetDetailAsync(post.Id)).Id);
        else await Assert.ThrowsAsync<NotFoundException>(() => service.GetDetailAsync(post.Id));
        Assert.Equal(allowed ? 1 : 0, writes);
        Assert.Equal(RentalPostStatus.Active, post.Status);
    }

    [Fact]
    public async Task Comparison_uses_Vietnam_day_and_keeps_today_tomorrow_and_unknown_dates()
    {
        var expired = Post(new DateOnly(2026, 10, 12));
        var today = Post(new DateOnly(2026, 10, 13));
        var tomorrow = Post(new DateOnly(2026, 10, 14));
        var unknown = Post(null);
        IReadOnlyList<RentalPost> candidates = [expired, today, tomorrow, unknown];
        var posts = Stub<IRentalPostRepository>((_, _) => Task.FromResult(candidates));
        var user = Guid.NewGuid();
        var profiles = Profiles(expired.OwnerId, user, UserRole.Renter);
        var service = new RentalDecisionService(posts, new UserContext(new CurrentUser(user), profiles), null!, null!, new Clock(), profiles);
        var response = await service.CompareAsync(new(candidates.Select(post => post.Id).ToArray()), CancellationToken.None);
        Assert.Equal(new[] { today.Id, tomorrow.Id, unknown.Id }, response.Posts.Select(item => item.Post.Id));
        Assert.Equal(expired.Id, Assert.Single(response.UnavailablePostIds));
        Assert.All(candidates, post => Assert.Equal(RentalPostStatus.Active, post.Status));
    }

    [Theory]
    [InlineData("appointment")]
    [InlineData("conversation")]
    [InlineData("invitation")]
    public async Task New_contact_actions_reject_expired_transfer_before_writing(string action)
    {
        var post = Post(new DateOnly(2026, 10, 12));
        var user = Guid.NewGuid();
        var profiles = Profiles(post.OwnerId, user, UserRole.Renter);
        var context = new UserContext(new CurrentUser(user), profiles);
        var posts = Stub<IRentalPostRepository>((method, _) => method.Name == nameof(IRentalPostRepository.GetByIdAsync)
            ? Task.FromResult<RentalPost?>(post) : throw new InvalidOperationException(method.Name));
        var serviceType = action switch { "appointment" => typeof(ViewingAppointmentService), "conversation" => typeof(PostConversationService), _ => typeof(RoommateInvitationService) };
        var arguments = serviceType.GetConstructors().Single().GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(UserContext)) return (object)context;
            if (parameter.ParameterType == typeof(TimeProvider)) return new Clock();
            if (parameter.ParameterType == typeof(IRentalPostRepository)) return posts;
            if (parameter.ParameterType == typeof(IUserProfileRepository)) return profiles;
            var proxy = DispatchProxy.Create(parameter.ParameterType, typeof(StrictStub));
            ((StrictStub)proxy).Handler = (method, _) => throw new InvalidOperationException("Unexpected action/write: " + method.Name);
            return proxy;
        }).ToArray();
        var service = Activator.CreateInstance(serviceType, arguments)!;
        await Assert.ThrowsAsync<NotFoundException>(async () =>
        {
            if (service is ViewingAppointmentService appointment) await appointment.CreateAsync(post.Id, new(Now.AddDays(1), null));
            else if (service is PostConversationService conversation) await conversation.StartRentalConversationAsync(post.Id);
            else await ((RoommateInvitationService)service).CreateAsync(post.Id, new(Guid.NewGuid()));
        });
        Assert.Equal(RentalPostStatus.Active, post.Status);
    }

    [Fact]
    public async Task Saved_public_list_omits_expired_transfer_without_deleting_saved_preferences()
    {
        var expired = Post(new DateOnly(2026, 10, 12));
        var today = Post(new DateOnly(2026, 10, 13));
        var user = Guid.NewGuid();
        IReadOnlyList<SavedPost> preferences = [new(user, expired.Id, Now), new(user, today.Id, Now)];
        var saves = Stub<ISavedPostRepository>((method, _) => method.Name == nameof(ISavedPostRepository.GetByUserAsync)
            ? Task.FromResult(preferences) : throw new InvalidOperationException("Unexpected saved write: " + method.Name));
        var posts = Stub<IRentalPostRepository>((method, _) => method.Name switch
        {
            nameof(IRentalPostRepository.GetByIdsWithMediaAsync) => Task.FromResult<IReadOnlyList<RentalPost>>([expired, today]),
            nameof(IRentalPostRepository.GetByIdWithMediaAsync) => Task.FromResult<RentalPost?>(expired),
            _ => throw new InvalidOperationException(method.Name),
        });
        var profiles = Profiles(expired.OwnerId, user, UserRole.Renter);
        var service = new SavedPostService(new UserContext(new CurrentUser(user), profiles), saves, posts, profiles, new Clock());
        Assert.Equal(today.Id, Assert.Single(await service.GetMineAsync()).Id);
        await Assert.ThrowsAsync<NotFoundException>(() => service.SaveAsync(expired.Id));
        Assert.Equal(2, preferences.Count);
        Assert.Equal(RentalPostStatus.Active, expired.Status);
    }

    private static IUserProfileRepository Profiles(Guid owner, Guid? viewer, UserRole viewerRole) =>
        Stub<IUserProfileRepository>((method, args) => method.Name switch
        {
            nameof(IUserProfileRepository.GetByIdsAsync) => Task.FromResult<IReadOnlyList<UserProfile>>([]),
            nameof(IUserProfileRepository.GetByIdAsync) => Task.FromResult<UserProfile?>(Profile((Guid)args![0]!, owner, viewer, viewerRole)),
            _ => throw new InvalidOperationException(method.Name),
        });

    private static UserProfile Profile(Guid id, Guid owner, Guid? viewer, UserRole viewerRole)
    {
        var profile = UserProfile.Create(id, "Read policy", Now);
        profile.SetRole(id == viewer && id != owner ? viewerRole : UserRole.Renter, Now);
        return profile;
    }

    private static RentalPost Post(DateOnly? leaseEnd)
    {
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.RoomTransfer, Now.AddDays(-3));
        // Persisted read model; date/status fields need not be re-submitted merely to inspect expiry.
        foreach (var pair in new Dictionary<string, object?>
        {
            [nameof(RentalPost.Status)] = RentalPostStatus.Active, [nameof(RentalPost.OriginalLeaseEndsOn)] = leaseEnd,
            [nameof(RentalPost.Price)] = 3_000_000m, [nameof(RentalPost.Area)] = 25m,
            [nameof(RentalPost.Latitude)] = 10.85m, [nameof(RentalPost.Longitude)] = 106.8m,
            [nameof(RentalPost.Address)] = "Long Thạnh Mỹ, Thủ Đức", [nameof(RentalPost.Title)] = "Pass phòng",
        }) typeof(RentalPost).GetProperty(pair.Key)!.SetValue(post, pair.Value);
        return post;
    }

    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, StrictStub>();
        ((StrictStub)(object)proxy).Handler = handler;
        return proxy;
    }
    public class StrictStub : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
