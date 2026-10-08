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
            new PostConversationRepository(db), TimeProvider.System, new LocalPublisher());

        await Saves(sender).SaveAsync(post.Id);
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Invitations(sender).CreateAsync(post.Id, new CreateRoommateInvitationDto(receiver)));
        await Saves(receiver).SaveAsync(post.Id);
        db.ChangeTracker.Clear();
        var candidate = Assert.Single(await Saves(sender).GetRoommateCandidatesAsync(post.Id));
        Assert.Equal(receiver, candidate.UserId);
        Assert.Equal(100, candidate.MatchScore);
        Assert.Equal("Linh Trung", candidate.PreferredArea);
        var invitation = await Invitations(sender).CreateAsync(post.Id, new CreateRoommateInvitationDto(receiver));
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
