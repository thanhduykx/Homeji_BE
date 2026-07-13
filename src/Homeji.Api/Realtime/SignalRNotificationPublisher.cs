using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Mappers.Notifications;
using Homeji.Domain.Entities;
using Microsoft.AspNetCore.SignalR;

namespace Homeji.Api.Realtime;

public sealed class SignalRNotificationPublisher : INotificationRealtimePublisher
{
    private static readonly Action<ILogger, Guid, Guid, Exception?> PublishFailed =
        LoggerMessage.Define<Guid, Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(PublishAsync)),
            "Could not publish notification {NotificationId} to user {RecipientId}.");

    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationPublisher> _logger;

    public SignalRNotificationPublisher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationPublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await _hubContext.Clients
                .User(notification.RecipientId.ToString())
                .SendAsync("notificationReceived", NotificationMapper.ToDto(notification), cancellationToken);
        }
        catch (Exception exception)
        {
            PublishFailed(_logger, notification.Id, notification.RecipientId, exception);
        }
    }
}
