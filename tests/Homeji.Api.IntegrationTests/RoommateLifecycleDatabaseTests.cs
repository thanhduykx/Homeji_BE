using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Roommates;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Roommates;
using Homeji.Application.Services.SavedPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class RoommateLifecycleDatabaseTests
{
    // Shares the isolated-loopback prerequisite with the marketplace database checks.
    [LocalMarketplaceDatabaseFact]
    public async Task Two_renters_save_find_invite_and_accept_with_a_persisted_exact_conversation()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")
            ?? throw new InvalidOperationException("HOMEJI_TEST_DATABASE is required.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Tests require the disposable loopback homeji_quality database.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var sender = await AddRenterAsync(db, now);
        var receiver = await AddRenterAsync(db, now);
        var post = RentalPost.CreateDraft(sender, RentalPostType.RoommateShare, now);
        post.UpdateDetails(RentalPostType.RoommateShare, "Phòng ở ghép kiểm thử", "Phòng có một chỗ còn trống",
            1_500_000, 0, 25, "Linh Trung", 10.85m, 106.77m, [], now, maxOccupants: 2, availableSlots: 1);
        for (var index = 0; index < 3; index++) post.AddMedia(MediaType.Image, "homeji-media", $"{sender}/qa-{index}.jpg", index == 0, index, now);
        post.Submit(now);
        post.Approve(now);
        db.RentalPosts.Add(post);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var profiles = new UserProfileRepository(db);
        var saved = new SavedPostRepository(db);
        var posts = new RentalPostRepository(db);
        UserContext Context(Guid user) => new(new LocalRenter(user), profiles);
        SavedPostService Saves(Guid user) => new(Context(user), saved, posts, profiles, TimeProvider.System);
        RoommateInvitationService Invitations(Guid user) => new(Context(user), new RoommateInvitationRepository(db),
            saved, posts, new NotificationRepository(db), new RoommateConversationRepository(db),
            new PostConversationRepository(db), TimeProvider.System, new LocalPublisher(), new RoommateDirectoryRepository(db), profiles);

        await Saves(sender).SaveAsync(post.Id);
        db.ChangeTracker.Clear();
        var listingProfile = new RoommateProfile(receiver);
        listingProfile.Update(RoommateIntent.SeekingAccommodation, true, null, now);
        await new RoommateDirectoryRepository(db).SaveAsync(listingProfile);
        db.ChangeTracker.Clear();
        var invitation = await Invitations(sender).CreateAsync(post.Id, new CreateRoommateInvitationDto(receiver));
        db.ChangeTracker.Clear();
        await Saves(receiver).SaveAsync(post.Id);
        db.ChangeTracker.Clear();
        var candidate = Assert.Single(await Saves(sender).GetRoommateCandidatesAsync(post.Id));
        Assert.Equal(receiver, candidate.UserId);
        Assert.Equal(100, candidate.MatchScore);
        Assert.Equal("Linh Trung", candidate.PreferredArea);
        db.ChangeTracker.Clear();
        Assert.Equal(invitation.Id, Assert.Single(await Invitations(receiver).GetMineAsync()).Id);
        await Assert.ThrowsAsync<RequestValidationException>(() => Invitations(sender).CreateAsync(post.Id, new CreateRoommateInvitationDto(receiver)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Invitations(sender).AcceptAsync(invitation.Id));
        db.ChangeTracker.Clear();
        var accepted = await Invitations(receiver).AcceptAsync(invitation.Id);
        db.ChangeTracker.Clear();
        Assert.Equal(RoommateInvitationStatus.Accepted, accepted.Status);
        Assert.NotNull(accepted.ConversationId);
        Assert.Equal(accepted.ConversationId, Assert.Single(await Invitations(sender).GetMineAsync()).ConversationId);
        Assert.Equal(accepted.ConversationId, Assert.Single(await Invitations(receiver).GetMineAsync()).ConversationId);
        var conversation = await db.PostConversations.SingleAsync(item => item.Id == accepted.ConversationId);
        Assert.Equal(post.Id, conversation.SubjectId);
        Assert.True(conversation.Includes(sender) && conversation.Includes(receiver));
        Assert.Equal(1, await db.RoommateConversations.CountAsync(item => item.InvitationId == invitation.Id));
        Assert.Equal(post.Title, accepted.RentalPostTitle);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Independent_directory_requires_consent_and_connects_without_listing_or_saved_posts()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Disposable loopback database required.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var sender = await AddRenterAsync(db, now);
        var receiver = await AddRenterAsync(db, now);
        var hidden = await AddRenterAsync(db, now);
        db.ChangeTracker.Clear();
        var profiles = new UserProfileRepository(db);
        var directory = new RoommateDirectoryRepository(db);
        UserContext Context(Guid user) => new(new LocalRenter(user), profiles);
        RoommateDirectoryService Directory(Guid user) => new(Context(user), directory, TimeProvider.System);
        RoommateInvitationService Invitations(Guid user) => new(Context(user), new RoommateInvitationRepository(db),
            new SavedPostRepository(db), new RentalPostRepository(db), new NotificationRepository(db),
            new RoommateConversationRepository(db), new PostConversationRepository(db), TimeProvider.System,
            new LocalPublisher(), directory, profiles);
        Assert.False((await Directory(receiver).GetMineAsync()).IsDiscoverable);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Invitations(sender).CreateIndependentAsync(new(receiver)));
        await Directory(receiver).UpdateMineAsync(new(RoommateIntent.HasAccommodation, true, "Đã thuê phòng, tìm người ở cùng"));
        db.ChangeTracker.Clear();
        await Directory(sender).UpdateMineAsync(new(RoommateIntent.SeekingAccommodation, true, null));
        db.ChangeTracker.Clear();
        Assert.Equal(sender, Assert.Single((await Directory(receiver).SearchAsync(RoommateIntent.SeekingAccommodation, null, 1, 20)).Items).UserId);
        var results = await Directory(sender).SearchAsync(RoommateIntent.HasAccommodation, "Linh Trung", 1, 20);
        var candidate = Assert.Single(results.Items);
        Assert.Equal(receiver, candidate.UserId);
        Assert.DoesNotContain(results.Items, x => x.UserId == hidden || x.UserId == sender);
        Assert.Equal(100, candidate.CompatibilityScore);
        Assert.Equal(RoommateIntent.HasAccommodation, candidate.Intent);
        await Assert.ThrowsAsync<RequestValidationException>(() => Directory(sender).SearchAsync(null, null, 0, 20));
        await Assert.ThrowsAsync<Homeji.Domain.Exceptions.DomainException>(() => Directory(receiver).UpdateMineAsync(new((RoommateIntent)99, true, null)));
        await Assert.ThrowsAsync<Homeji.Domain.Exceptions.DomainException>(() => Directory(receiver).UpdateMineAsync(new(RoommateIntent.HasAccommodation, true, new string('x', 601))));
        var invitation = await Invitations(sender).CreateIndependentAsync(new(receiver));
        db.ChangeTracker.Clear();
        Assert.Null(invitation.RentalPostId);
        var invitationsRepository = new RoommateInvitationRepository(db);
        await invitationsRepository.AddAsync(new RoommateInvitation(null, receiver, sender, now));
        await Assert.ThrowsAsync<RequestValidationException>(() => invitationsRepository.SaveChangesAsync());
        db.ChangeTracker.Clear();
        Assert.Equal(invitation.Id, (await Invitations(sender).CreateIndependentAsync(new(receiver))).Id);
        Assert.Equal(invitation.Id, (await Invitations(receiver).CreateIndependentAsync(new(sender))).Id);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Invitations(sender).CreateIndependentAsync(new(sender)));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Invitations(sender).AcceptAsync(invitation.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Invitations(hidden).AcceptAsync(invitation.Id));
        var accepted = await Invitations(receiver).AcceptAsync(invitation.Id);
        db.ChangeTracker.Clear();
        Assert.NotNull(accepted.ConversationId);
        var chat = await db.PostConversations.SingleAsync(x => x.Id == accepted.ConversationId);
        Assert.Equal(ConversationSubjectType.RoommateInvitation, chat.SubjectType);
        Assert.Equal(invitation.Id, chat.SubjectId);
        Assert.True(chat.Includes(sender) && chat.Includes(receiver));
        Homeji.Application.Services.Conversations.PostConversationService Conversations(Guid user) => new(
            Context(user), new PostConversationRepository(db), new RentalPostRepository(db),
            new MarketplacePostRepository(db), new RentalWantedPostRepository(db), profiles,
            new NotificationRepository(db), new LocalPublisher(), TimeProvider.System,
            imageProcessor: null!, new MarketplaceOrderRepository(db));
        var message = await Conversations(sender).SendMessageAsync(chat.Id, new("Chào bạn, mình muốn trao đổi nhu cầu ở ghép."));
        db.ChangeTracker.Clear();
        var received = Assert.Single(await Conversations(receiver).GetMessagesAsync(chat.Id));
        Assert.Equal(message.Id, received.Id);
        Assert.Equal(sender, received.SenderId);
        Assert.Equal(message.Body, received.Body);
        Assert.Equal(chat.Id, received.ConversationId);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Conversations(hidden).GetMessagesAsync(chat.Id));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Conversations(hidden).SendMessageAsync(chat.Id, new("Không được gửi")));
        Assert.Equal(accepted.ConversationId, (await Invitations(sender).CreateIndependentAsync(new(receiver))).ConversationId);
        await Directory(receiver).UpdateMineAsync(new(RoommateIntent.HasAccommodation, false, null));
        db.ChangeTracker.Clear();
        Assert.Empty((await Directory(sender).SearchAsync(null, null, 1, 20)).Items);
        Assert.Equal(accepted.ConversationId, Assert.Single(await Invitations(sender).GetMineAsync()).ConversationId);
        Assert.Equal("Người thuê kiểm thử", Assert.Single(await Invitations(sender).GetMineAsync()).ReceiverDisplayName);
    }

    private static async Task<Guid> AddRenterAsync(ApplicationDbContext db, DateTimeOffset now)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({id})");
        var profile = UserProfile.Create(id, "Người thuê kiểm thử", now);
        profile.UpdateLifestyle(UserRole.Renter, (SleepHabit)1, (PetPreference)1, (SmokingPreference)1, 2_000_000, "Linh Trung", now);
        db.UserProfiles.Add(profile);
        await db.SaveChangesAsync();
        return id;
    }

    private sealed class LocalRenter(Guid user) : ICurrentUser { public Guid? UserId => user; }
    private sealed class LocalPublisher : INotificationRealtimePublisher
    {
        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
