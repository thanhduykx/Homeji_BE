using Homeji.Application.Abstractions.Notifications;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Notifications;

internal sealed class NoOpNotificationRealtimePublisher : INotificationRealtimePublisher
{
    public Task PublishAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
