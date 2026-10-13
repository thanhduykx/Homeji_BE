using System.Reflection;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.Moderation;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.Admin;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalTransferPublicationTests
{
    // Midnight in Vietnam, while the UTC date is still October 12.
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Submit_rejects_expired_transfer_without_changing_draft_or_saving()
    {
        var post = CompleteTransfer(new DateOnly(2026, 10, 12));
        var writes = 0;
        var service = Service<RentalPostService>(post, post.OwnerId, UserRole.Renter, () => writes++);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.SubmitAsync(post.Id));

        Assert.Contains("hết hạn", Assert.Single(error.Errors["originalLeaseEndsOn"]));
        Assert.Equal(RentalPostStatus.Draft, post.Status);
        Assert.Equal(0, writes);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    public async Task Submit_accepts_transfer_ending_today_or_later_in_Vietnam(int leaseEndDay)
    {
        var post = CompleteTransfer(new DateOnly(2026, 10, leaseEndDay));
        var writes = 0;
        var service = Service<RentalPostService>(post, post.OwnerId, UserRole.Renter, () => writes++);

        var result = await service.SubmitAsync(post.Id);

        Assert.Equal(RentalPostStatus.Pending, result.Status);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task Approval_rejects_expired_pending_transfer_without_mutation_or_notifications()
    {
        var post = CompleteTransfer(new DateOnly(2026, 10, 12));
        post.Submit(Now.AddDays(-1));
        var writes = 0;
        var service = Service<AdminModerationService>(post, Guid.NewGuid(), UserRole.Admin, () => writes++);

        var error = await Assert.ThrowsAsync<RequestValidationException>(() => service.ApproveRentalPostAsync(
            post.Id, new("Đã xác nhận với chủ nhà.")));

        Assert.Contains("hết hạn", Assert.Single(error.Errors["originalLeaseEndsOn"]));
        Assert.Equal(RentalPostStatus.Pending, post.Status);
        Assert.Null(post.OwnerConsentVerifiedAt);
        Assert.Null(post.OwnerConsentVerifiedBy);
        Assert.Equal(0, writes);
    }

    [Theory]
    [InlineData(13)]
    [InlineData(14)]
    public async Task Approval_accepts_transfer_ending_today_or_later_in_Vietnam(int leaseEndDay)
    {
        var post = CompleteTransfer(new DateOnly(2026, 10, leaseEndDay));
        post.Submit(Now.AddDays(-1));
        var adminId = Guid.NewGuid();
        var writes = 0;
        var service = Service<AdminModerationService>(post, adminId, UserRole.Admin, () => writes++);

        var result = await service.ApproveRentalPostAsync(post.Id, new("Đã xác nhận với chủ nhà."));

        Assert.Equal(RentalPostStatus.Active, result.Status);
        Assert.Equal(adminId, post.OwnerConsentVerifiedBy);
        Assert.Equal(Now, post.OwnerConsentVerifiedAt);
        Assert.True(writes > 0);
    }

    private static RentalPost CompleteTransfer(DateOnly leaseEnd)
    {
        var createdAt = Now.AddDays(-10);
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.RoomTransfer, createdAt);
        post.UpdateDetails(RentalPostType.RoomTransfer, "Pass phòng gần trường",
            "Phòng còn hợp đồng và cần chuyển cho sinh viên khác.", 3_000_000, 3_000_000,
            24, "Linh Trung, Thủ Đức", 10.85m, 106.77m, [], createdAt,
            availableFrom: new DateOnly(2026, 10, 5), transferKind: RoomTransferKind.LeaseAssignment,
            originalLeaseEndsOn: leaseEnd, passFee: 0, transferReason: "Chuyển nơi học.",
            ownerConsentConfirmed: true, ownerConsentContact: "owner@example.com");
        for (var i = 0; i < RentalPost.MinimumImageCountForSubmit; i++)
            post.AddMedia(MediaType.Image, "rental", $"image-{i}.jpg", i == 0, i, createdAt);
        return post;
    }

    private static T Service<T>(RentalPost post, Guid userId, UserRole role, Action save) where T : class
    {
        var profile = UserProfile.Create(userId, "Publication test", Now);
        profile.SetRole(role, Now);
        var profiles = Stub<IUserProfileRepository>((method, _) => method.Name switch
        {
            nameof(IUserProfileRepository.GetByIdAsync) => Task.FromResult<UserProfile?>(profile),
            nameof(IUserProfileRepository.GetMatchingRentersAsync) => Task.FromResult<IReadOnlyList<UserProfile>>([]),
            _ => throw new InvalidOperationException(method.Name),
        });
        var posts = Stub<IRentalPostRepository>((method, _) => method.Name switch
        {
            nameof(IRentalPostRepository.GetByIdWithMediaAsync) => Task.FromResult<RentalPost?>(post),
            nameof(IRentalPostRepository.SaveChangesAsync) => Save(),
            _ => throw new InvalidOperationException(method.Name),
        });
        Task Save() { save(); return Task.CompletedTask; }
        var context = new UserContext(new CurrentUser(userId), profiles);
        var moderation = new ContentModerationService(Stub<IBadWordRepository>((_, _) => Task.FromResult<IReadOnlyList<string>>([])));
        var arguments = typeof(T).GetConstructors().Single().GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(UserContext)) return (object)context;
            if (parameter.ParameterType == typeof(TimeProvider)) return new Clock();
            if (parameter.ParameterType == typeof(IRentalPostRepository)) return posts;
            if (parameter.ParameterType == typeof(IUserProfileRepository)) return profiles;
            if (parameter.ParameterType == typeof(ContentModerationService)) return moderation;
            if (parameter.ParameterType == typeof(INotificationRepository))
                return Stub<INotificationRepository>((method, _) => method.Name == nameof(INotificationRepository.AddAsync)
                    ? Save() : throw new InvalidOperationException(method.Name));
            if (parameter.ParameterType == typeof(INotificationRealtimePublisher))
                return Stub<INotificationRealtimePublisher>((_, _) => Save());
            if (parameter.ParameterType == typeof(ISavedPostRepository))
                return Stub<ISavedPostRepository>((method, _) => method.Name == nameof(ISavedPostRepository.GetByPostAsync)
                    ? Task.FromResult<IReadOnlyList<SavedPost>>([]) : throw new InvalidOperationException(method.Name));
            return null!;
        }).ToArray();
        return (T)Activator.CreateInstance(typeof(T), arguments)!;
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
