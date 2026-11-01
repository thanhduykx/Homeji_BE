using Homeji.Domain.Entities;

namespace Homeji.Application.Abstractions.Notifications;

public interface IMaintenanceAnnouncements
{
    Task PublishAsync(MaintenanceAnnouncement announcement, CancellationToken cancellationToken);
    Task<MaintenanceAnnouncement?> GetCurrentAsync(CancellationToken cancellationToken);
}
