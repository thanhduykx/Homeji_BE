using Homeji.Application.DTOs.Notifications;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Notifications;

public static class NotificationMapper
{
    public static NotificationDto ToDto(Notification notification)
    {
        return new NotificationDto(
            notification.Id,
            notification.Type,
            notification.Title,
            notification.Message,
            notification.RelatedEntityId,
            notification.IsRead,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
