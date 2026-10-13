using System.Reflection;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.AI;
using Homeji.Application.Services.AI;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.AI;

public sealed class AiSearchTransferExpiryTests
{
    // Noon in Vietnam: a lease that ended yesterday is expired under either proposed boundary policy.
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 5, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Search_never_recommends_yesterdays_expired_transfer_or_uses_it_for_map_focus()
    {
        var expired = Transfer(new DateOnly(2026, 10, 12));
        var valid = Transfer(new DateOnly(2026, 10, 20));
        var response = await Service([expired, valid]).HighlightRentalPostsAsync(new("Phòng Thủ Đức dưới 4tr",
            Criteria: new AiParsedSearchCriteriaDto("Thủ Đức", null, null, 4_000_000, null, null, [])));
        Assert.Equal(valid.Id, Assert.Single(response.Posts).Post.Id);
        Assert.DoesNotContain(response.Posts.SelectMany(post => post.Evidence), evidence => evidence.PostId == expired.Id);
        Assert.Equal(response.Posts.Single().Post.Latitude, response.MapFocusLatitude);
        Assert.Equal(response.Posts.Single().Post.Longitude, response.MapFocusLongitude);
    }

    [Fact]
    public async Task Only_expired_candidates_produce_no_cards_evidence_or_map_focus()
    {
        var response = await Service([Transfer(new DateOnly(2026, 10, 12))]).HighlightRentalPostsAsync(new("Phòng Thủ Đức",
            Criteria: new AiParsedSearchCriteriaDto("Thủ Đức", null, null, null, null, null, [])));
        Assert.Empty(response.Posts);
        Assert.Null(response.MapFocusLatitude);
        Assert.Null(response.MapFocusLongitude);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(null)]
    public async Task Search_keeps_end_day_future_and_unknown_lease_dates(int? offset)
    {
        var post = Transfer(new DateOnly(2026, 10, 20));
        typeof(RentalPost).GetProperty(nameof(RentalPost.OriginalLeaseEndsOn))!.SetValue(post,
            offset.HasValue ? new DateOnly(2026, 10, 13).AddDays(offset.Value) : null);
        var response = await Service([post]).HighlightRentalPostsAsync(new("Phòng Thủ Đức",
            Criteria: new AiParsedSearchCriteriaDto("Thủ Đức", null, null, null, null, null, [])));
        Assert.Equal(post.Id, Assert.Single(response.Posts).Post.Id);
        Assert.Equal(RentalPostStatus.Active, post.Status);
    }

    private static AiSearchService Service(IReadOnlyList<RentalPost> candidates)
    {
        var posts = Stub<IRentalPostRepository>((method, _) => method.Name == nameof(IRentalPostRepository.SearchActiveAsync)
            ? Task.FromResult(candidates) : throw new InvalidOperationException(method.Name));
        var subscriptions = Stub<IUserSubscriptionRepository>((method, _) => method.Name == nameof(IUserSubscriptionRepository.GetActivePremiumByUserIdsAsync)
            ? Task.FromResult<IReadOnlyDictionary<Guid, UserSubscription>>(new Dictionary<Guid, UserSubscription>())
            : throw new InvalidOperationException(method.Name));
        var parser = Stub<IAiSearchTextParser>((method, _) => throw new InvalidOperationException(method.Name));
        return new(parser, posts, subscriptions, null!, Options.Create(new AiSearchOptions()), new FixedClock());
    }

    private static RentalPost Transfer(DateOnly leaseEndsOn)
    {
        var createdAt = Now.AddDays(-3);
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.RoomTransfer, createdAt);
        post.UpdateDetails(RentalPostType.RoomTransfer, "Pass phòng Thủ Đức", "Chủ nhà đồng ý chuyển hợp đồng.",
            3_000_000, 3_000_000, 25, "12 Đường A, Long Thạnh Mỹ, Thủ Đức", 10.85m, 106.8m, [], createdAt,
            availableFrom: new DateOnly(2026, 10, 11), transferKind: RoomTransferKind.LeaseAssignment,
            originalLeaseEndsOn: leaseEndsOn, transferReason: "Chuyển nơi học", ownerConsentConfirmed: true,
            ownerConsentContact: "owner@example.test");
        for (var index = 0; index < 3; index++) post.AddMedia(MediaType.Image, "test", $"image-{index}.jpg", index == 0, index, createdAt);
        post.Submit(createdAt);
        post.Approve(createdAt, Guid.NewGuid(), "Chủ nhà đã đồng ý");
        return post;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var value = DispatchProxy.Create<T, StrictStub>();
        ((StrictStub)(object)value).Handler = handler;
        return value;
    }

    public class StrictStub : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
