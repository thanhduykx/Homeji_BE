using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Abstractions.Presence;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.Services.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class MaintenanceAnnouncementDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Admin_publication_survives_reopen_and_rejects_non_admin_and_past_end()
    {
        await using var db = Open();
        await db.MaintenanceAnnouncements.ExecuteDeleteAsync();
        var now = DateTimeOffset.UtcNow;
        var clock = new Clock(now);
        var profiles = new UserProfileRepository(db);
        var admin = await Seed(db, UserRole.Admin);
        var renter = await Seed(db, UserRole.Renter);
        var store = new MaintenanceAnnouncements(db, clock);
        var request = new MaintenanceAnnouncementRequestDto("Bảo trì thanh toán", "Nâng cấp hệ thống thanh toán.", now.AddHours(1), now.AddHours(2));
        AdminModerationService Service(Guid userId) => new(new UserContext(new CurrentUser(userId), profiles),
            null!, null!, new NotificationRepository(db), null!, profiles, new OfflineUsers(), null!, null!, null!, null!, null!, null!, clock, new Realtime(), store);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(renter).SendMaintenanceAnnouncementAsync(request));
        await Assert.ThrowsAsync<RequestValidationException>(() => Service(admin).SendMaintenanceAnnouncementAsync(request with { ScheduledStartAt = now.AddHours(-2), ScheduledEndAt = now.AddHours(-1) }));
        var delivered = await Service(admin).SendMaintenanceAnnouncementAsync(request);
        Assert.True(delivered.RecipientCount >= 2);
        await using var reopened = Open();
        var current = await new MaintenanceAnnouncements(reopened, clock).GetCurrentAsync(default);
        Assert.NotNull(current);
        Assert.Equal(request.Message, current.Message);
        Assert.InRange((request.ScheduledEndAt!.Value - current.ExpiresAt).Duration(), TimeSpan.Zero, TimeSpan.FromMilliseconds(1));
        Assert.Contains(await new NotificationRepository(reopened).GetForUserAsync(renter, false), row => row.Type == NotificationType.MaintenanceAnnouncement && row.Title == request.Title);
        Assert.Null(await new MaintenanceAnnouncements(reopened, new Clock(now.AddHours(3))).GetCurrentAsync(default));
        Assert.True(await reopened.Database.SqlQueryRaw<bool>("SELECT relrowsecurity AS \"Value\" FROM pg_class WHERE oid='homeji.maintenance_announcements'::regclass").SingleAsync());
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Expired_replacement_does_not_restore_an_older_notice()
    {
        await using var db = Open();
        await db.MaintenanceAnnouncements.ExecuteDeleteAsync();
        var now = DateTimeOffset.UtcNow.AddDays(10);
        var store = new MaintenanceAnnouncements(db, new Clock(now));
        await store.PublishAsync(new MaintenanceAnnouncement { Title = "Cũ", Message = "Lịch cũ", CreatedAt = now, ExpiresAt = now.AddDays(1) }, default);
        await store.PublishAsync(new MaintenanceAnnouncement { Title = "Mới", Message = "Lịch thay thế", CreatedAt = now.AddMinutes(1), ExpiresAt = now.AddHours(1) }, default);
        Assert.Equal("Mới", (await store.GetCurrentAsync(default))!.Title);
        Assert.Null(await new MaintenanceAnnouncements(db, new Clock(now.AddHours(2))).GetCurrentAsync(default));
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE") ?? throw new InvalidOperationException("Local database required");
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host != "127.0.0.1" || parsed.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database only");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
    private static async Task<Guid> Seed(ApplicationDbContext db, UserRole role)
    {
        var id = Guid.NewGuid(); var email = id.ToString("N") + "@example.invalid";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({id},{email},now())");
        var profile = UserProfile.Create(id, "Fixture", DateTimeOffset.UtcNow);
        profile.SetRole(role, DateTimeOffset.UtcNow);
        db.UserProfiles.Add(profile); await db.SaveChangesAsync(); return id;
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class OfflineUsers : IOnlineUserTracker
    {
        public void Connected(Guid userId, string connectionId, DateTimeOffset connectedAt) { }
        public void Disconnected(string connectionId) { }
        public IReadOnlyDictionary<Guid, DateTimeOffset> GetOnlineUsers() => new Dictionary<Guid, DateTimeOffset>();
    }
    private sealed class Realtime : INotificationRealtimePublisher
    {
        public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
