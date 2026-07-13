using Homeji.Domain.Entities;

namespace Homeji.Application.Abstractions.Notifications;

public interface INotificationRealtimePublisher
{
    Task PublishAsync(Notification notification, CancellationToken cancellationToken = default);
}
