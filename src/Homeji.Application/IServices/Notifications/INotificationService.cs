using Homeji.Application.DTOs.Notifications;
using Homeji.Domain.Entities;

namespace Homeji.Application.IServices.Notifications;

public interface INotificationService
{
    Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetMineAsync(bool unreadOnly, CancellationToken cancellationToken = default);

    Task<NotificationDto> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
