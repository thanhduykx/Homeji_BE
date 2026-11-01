using System.Diagnostics;
using System.Text.Json;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Conversations;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit.Abstractions;

namespace Homeji.Api.IntegrationTests;

public sealed class ConversationDatabaseTests(ITestOutputHelper output)
{
    [LocalMarketplaceDatabaseFact]
    public async Task UserBlocksPersistAcrossThreadsAndOnlyBlockerCanRemoveTheirBlock()
    {
        await using var db = Open();
        var (first, second, conversation) = await Seed(db);
        Assert.True(await db.Database.SqlQueryRaw<bool>("SELECT relrowsecurity AS \"Value\" FROM pg_class WHERE oid='homeji.user_message_blocks'::regclass").SingleAsync());
        var a = Service(db, first); var b = Service(db, second);
        Assert.True((await a.SetUserBlockAsync(conversation.Id, true)).IsBlockedByMe);
        await a.SetUserBlockAsync(conversation.Id, true);
        Assert.Equal(1, await db.UserMessageBlocks.CountAsync(block => block.BlockerId == first));
        var state = await b.SetUserBlockAsync(conversation.Id, false);
        Assert.True(state.IsBlockedByOther);
        Assert.False(state.IsBlockedByMe);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => a.SendMessageAsync(conversation.Id, new SendPostMessageDto("blocked")));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => b.SendVoiceAsync(conversation.Id, []));
        var another = new PostConversation(ConversationSubjectType.MarketplacePost, Guid.NewGuid(), second, first, DateTimeOffset.UtcNow);
        db.PostConversations.Add(another); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => b.SendMessageAsync(another.Id, new SendPostMessageDto("also blocked")));
        Assert.All(await a.GetMineAsync(), row => Assert.True(row.IsBlockedByMe));
        await a.SetUserBlockAsync(conversation.Id, false);
        var sent = await b.SendMessageAsync(conversation.Id, new SendPostMessageDto("after unblock"));
        Assert.Equal("after unblock", sent.Body);
        Assert.Single(await a.GetMessagesAsync(conversation.Id));
    }

    [LocalMarketplaceDatabaseFact]
    public async Task HistoryWithLargeAttachmentsReturnsMetadataAndUnreadUsesReadCutoff()
    {
        await using var db = Open();
        var (first, second, conversation) = await Seed(db);
        var now = DateTimeOffset.UtcNow;
        for (var index = 0; index < 8; index++)
        {
            var message = new PostMessage(conversation.Id, second, "history", now.AddSeconds(index));
            message.AddImage(second, MessageAttachmentContext.Other, "image/jpeg", new byte[256 * 1024], 1, 1, new string('a', 64), now);
            db.PostMessages.Add(message);
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new PostConversationRepository(db);
        var timer = Stopwatch.StartNew();
        var oldRead = await db.PostMessages.AsNoTracking().Include(message => message.Attachments).Where(message => message.ConversationId == conversation.Id).ToArrayAsync();
        var oldMilliseconds = timer.Elapsed.TotalMilliseconds;
        var oldBytes = oldRead.SelectMany(message => message.Attachments).Sum(attachment => attachment.Content.Length);
        timer.Restart();
        var history = await repository.GetMessagesAsync(conversation.Id);
        var newMilliseconds = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(8, history.Count);
        Assert.All(history, message => Assert.Single(message.Attachments!));
        Assert.All(history, message => Assert.Equal(256 * 1024, message.Attachments![0].Bytes));
        var json = JsonSerializer.Serialize(history);
        Assert.True(json.Length < oldBytes / 20);
        var threads = await repository.GetForUserAsync(first);
        Assert.Equal(8, (await repository.CountUnreadByConversationIdsAsync(first, threads))[conversation.Id]);
        var tracked = await repository.GetByIdAsync(conversation.Id);
        tracked!.MarkRead(first, now.AddSeconds(3)); await repository.SaveChangesAsync();
        threads = await repository.GetForUserAsync(first);
        Assert.Equal(4, (await repository.CountUnreadByConversationIdsAsync(first, threads))[conversation.Id]);
        Assert.Empty(await repository.CountUnreadByConversationIdsAsync(second, threads));
        output.WriteLine($"Fixture old binary read: {oldMilliseconds:F1}ms / {oldBytes} bytes; metadata read: {newMilliseconds:F1}ms / {json.Length} JSON chars. Not a production latency claim.");
    }

    [LocalMarketplaceDatabaseFact]
    public async Task MessageAlreadyBeingPreparedCannotBypassCompletedBlock()
    {
        await using var db = Open();
        var (first, second, conversation) = await Seed(db);
        var repository = new PostConversationRepository(db);
        Assert.False(await repository.IsMessagingBlockedAsync(first, second));
        await using var otherDb = Open();
        await new PostConversationRepository(otherDb).SetUserBlockAsync(second, first, true, DateTimeOffset.UtcNow);
        await repository.AddMessageAsync(new PostMessage(conversation.Id, first, "prepared before block", DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => repository.SaveChangesAsync());
        Assert.False(await otherDb.PostMessages.AnyAsync(message => message.ConversationId == conversation.Id));
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE") ?? throw new InvalidOperationException("Disposable database required.");
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Local test database only.");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }

    private static async Task<(Guid First, Guid Second, PostConversation Conversation)> Seed(ApplicationDbContext db)
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({first}),({second})");
        db.UserProfiles.AddRange(UserProfile.Create(first, "Local first", now), UserProfile.Create(second, "Local second", now));
        var conversation = new PostConversation(ConversationSubjectType.RentalPost, Guid.NewGuid(), first, second, now);
        db.PostConversations.Add(conversation); await db.SaveChangesAsync();
        return (first, second, conversation);
    }

    private static PostConversationService Service(ApplicationDbContext db, Guid user)
    {
        var profiles = new UserProfileRepository(db);
        return new PostConversationService(new UserContext(new CurrentUser(user), profiles), new PostConversationRepository(db),
            null!, null!, null!, profiles, new NotificationRepository(db), new NoNotifications(), TimeProvider.System, null!, null!);
    }
    private sealed class CurrentUser(Guid id) : ICurrentUser { public Guid? UserId => id; }
    private sealed class NoNotifications : INotificationRealtimePublisher
    {
        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
