using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Conversations;
using Homeji.Application.Services.Activities;
using Homeji.Application.Services.Moderation;
using Homeji.Application.Services.RentalPosts;
using Homeji.Application.Services.RentalPosts.Validation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class RenterRoommatePostingDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Renter_authors_shared_accommodation_with_filtered_author_feed_and_durable_contact()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Disposable loopback database required.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var author = await AddUserAsync(db, UserRole.Renter);
        var seeker = await AddUserAsync(db, UserRole.Renter);
        var landlord = await AddUserAsync(db, UserRole.Landlord);
        var stranger = await AddUserAsync(db, UserRole.Renter);
        db.ChangeTracker.Clear();
        var profiles = new UserProfileRepository(db);
        UserContext Context(Guid id) => new(new TestUser(id), profiles);
        RentalPostService Posts(Guid id) => new(Context(id), new RentalPostRepository(db), new UserSubscriptionRepository(db),
            new UpdateRentalPostDtoValidator(), new AddRentalPostMediaDtoValidator(), new ContentModerationService(new BadWordRepository(db)),
            new UserActivityService(Context(id), new UserActivityRepository(db), TimeProvider.System),
            new RentalReviewRepository(db), profiles, new PostConversationRepository(db), new ViewingAppointmentRepository(db), TimeProvider.System);
        var draft = await Posts(author).CreateDraftAsync(new(RentalPostType.RoommateShare));
        db.ChangeTracker.Clear();
        Assert.Equal(author, draft.OwnerId);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Posts(seeker).UpdateAsync(draft.Id, Details(RentalPostType.RoommateShare)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Posts(author).CreateDraftAsync(new(RentalPostType.VacantRoom)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Posts(author).UpdateAsync(draft.Id, Details(RentalPostType.VacantRoom)));
        await Posts(author).UpdateAsync(draft.Id, Details(RentalPostType.RoommateShare));
        db.ChangeTracker.Clear();
        for (var index = 0; index < 3; index++)
        {
            await Posts(author).AddMediaAsync(draft.Id, new(MediaType.Image, "homeji-media", $"rental-posts/{author:D}/{draft.Id:D}/qa-{index}.jpg", index == 0, index));
            db.ChangeTracker.Clear();
        }
        Assert.Equal(RentalPostStatus.Pending, (await Posts(author).SubmitAsync(draft.Id)).Status);
        db.ChangeTracker.Clear();
        var pending = await db.RentalPosts.SingleAsync(x => x.Id == draft.Id);
        pending.Approve(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        // Newer landlord posts must not consume the renter page before filtering.
        foreach (var type in new[] { RentalPostType.RoommateShare, RentalPostType.VacantRoom })
        {
            var oldStyle = RentalPost.CreateDraft(landlord, type, DateTimeOffset.UtcNow);
            oldStyle.UpdateDetails(type, "Tin chủ trọ", "Phòng chủ trọ cho thuê", 2_000_000, 0, 25, "Linh Trung", 10.85m, 106.77m, [], DateTimeOffset.UtcNow);
            for (var index = 0; index < 3; index++) oldStyle.AddMedia(MediaType.Image, "homeji-media", $"rental-posts/{landlord}/{oldStyle.Id}/qa-{index}.jpg", index == 0, index, DateTimeOffset.UtcNow);
            oldStyle.Submit(DateTimeOffset.UtcNow); oldStyle.Approve(DateTimeOffset.UtcNow);
            db.RentalPosts.Add(oldStyle);
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var search = new RentalPostSearchDto(null, null, null, null, null, null, null, null, null, [], PageSize: 1,
            Type: RentalPostType.RoommateShare, OwnerRole: UserRole.Renter);
        var result = Assert.Single(await Posts(seeker).SearchAsync(search));
        Assert.Equal(draft.Id, result.Id);
        Assert.Equal(author, result.OwnerId);
        Assert.Equal(UserRole.Renter, result.OwnerRole);
        Assert.Equal("Sinh viên kiểm thử", result.OwnerDisplayName);
        Assert.Equal("UEL", result.OwnerSchool);
        Assert.Equal(1, result.AvailableSlots); Assert.Equal(2, result.MaxOccupants);
        var detail = await Posts(seeker).GetDetailAsync(draft.Id);
        db.ChangeTracker.Clear();
        Assert.Equal(UserRole.Renter, detail.OwnerRole);
        Assert.Equal("UEL", detail.OwnerSchool);
        Assert.Equal("Sinh viên kiểm thử", detail.OwnerDisplayName);
        Assert.DoesNotContain("<", result.DescriptionExcerpt!);
        Assert.Equal(draft.Id, Assert.Single(await Posts(seeker).SearchAsync(search with { Keyword = "uel" })).Id);
        Assert.Equal(2, (await Posts(seeker).SearchAsync(search with { OwnerRole = null, PageSize = 20 })).Count);
        await Assert.ThrowsAsync<RequestValidationException>(() => Posts(seeker).SearchAsync(search with { OwnerRole = UserRole.Admin }));
        PostConversationService Chat(Guid user) => new(Context(user), new PostConversationRepository(db),
            new RentalPostRepository(db), new MarketplacePostRepository(db), new RentalWantedPostRepository(db), profiles,
            new NotificationRepository(db), new Publisher(), TimeProvider.System, imageProcessor: null!, new MarketplaceOrderRepository(db));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Chat(author).StartRentalConversationAsync(draft.Id));
        var chat = await Chat(seeker).StartRentalConversationAsync(draft.Id);
        db.ChangeTracker.Clear();
        var sent = await Chat(seeker).SendMessageAsync(chat.Id, new("Mình muốn tìm hiểu chỗ ở ghép."));
        db.ChangeTracker.Clear();
        Assert.Equal(sent.Id, Assert.Single(await Chat(author).GetMessagesAsync(chat.Id)).Id);
        Assert.Equal(chat.Id, Assert.Single(await Chat(author).GetMineAsync()).Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Chat(stranger).GetMessagesAsync(chat.Id));
    }

    private static UpdateRentalPostDto Details(RentalPostType type) => new(type, "Mình tìm bạn ở ghép", "<p>Đã thuê phòng, tìm bạn cùng ở.</p>",
        1_500_000, 0, 25, "Linh Trung", 10.85m, 106.77m, [], MaxOccupants: 2, AvailableSlots: 1);
    private static async Task<Guid> AddUserAsync(ApplicationDbContext db, UserRole role)
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({id})");
        var profile = UserProfile.Create(id, "Sinh viên kiểm thử", now);
        profile.SetRole(role, now);
        profile.UpdateProfile("Sinh viên kiểm thử", null, null, "UEL", "Linh Trung", null, null, now);
        db.UserProfiles.Add(profile); await db.SaveChangesAsync(); return id;
    }
    private sealed class TestUser(Guid id) : ICurrentUser { public Guid? UserId => id; }
    private sealed class Publisher : INotificationRealtimePublisher
    {
        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
