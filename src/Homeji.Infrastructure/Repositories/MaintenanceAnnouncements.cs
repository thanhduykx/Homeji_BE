using Homeji.Application.Abstractions.Notifications;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class MaintenanceAnnouncements(ApplicationDbContext db, TimeProvider clock) : IMaintenanceAnnouncements
{
    public async Task PublishAsync(MaintenanceAnnouncement announcement, CancellationToken cancellationToken)
    {
        db.MaintenanceAnnouncements.Add(announcement);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<MaintenanceAnnouncement?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        // A newer notice replaces the previous schedule, including once it expires.
        var latest = await db.MaintenanceAnnouncements.AsNoTracking()
            .OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        return latest is not null && latest.ExpiresAt > clock.GetUtcNow() ? latest : null;
    }
}
