using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Notifications;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IServices.Notifications;
using Homeji.Application.Mappers.Notifications;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Notifications;

public sealed class NotificationService : INotificationService
{
    private readonly UserContext _userContext;
    private readonly INotificationRepository _notifications;
    private readonly TimeProvider _timeProvider;

    public NotificationService(
        UserContext userContext,
        INotificationRepository notifications,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _notifications = notifications;
        _timeProvider = timeProvider;
    }

    public async Task NotifyAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _notifications.AddAsync(notification, cancellationToken);
        await _notifications.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetMineAsync(bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var notifications = await _notifications.GetForUserAsync(userId, unreadOnly, cancellationToken);
        return notifications.Select(NotificationMapper.ToDto).ToArray();
    }

    public async Task<NotificationDto> MarkReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var notification = await _notifications.GetByIdAsync(notificationId, cancellationToken)
            ?? throw new NotFoundException(nameof(Notification), notificationId);
        UserContext.EnsureOwner(userId, notification.RecipientId);
        notification.MarkRead(_timeProvider.GetUtcNow());
        await _notifications.SaveChangesAsync(cancellationToken);
        return NotificationMapper.ToDto(notification);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var notifications = await _notifications.GetForUserAsync(userId, unreadOnly: true, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        foreach (var notification in notifications)
        {
            notification.MarkRead(now);
        }

        await _notifications.SaveChangesAsync(cancellationToken);
    }
}
