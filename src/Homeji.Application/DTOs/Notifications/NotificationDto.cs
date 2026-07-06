using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Notifications;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    Guid? RelatedEntityId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);
