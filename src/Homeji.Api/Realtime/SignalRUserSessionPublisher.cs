using Homeji.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.SignalR;

namespace Homeji.Api.Realtime;

public sealed class SignalRUserSessionPublisher(IHubContext<NotificationHub> hubContext)
    : IUserSessionRealtimePublisher
{
    public Task TerminateAsync(
        Guid userId,
        string reason,
        DateTimeOffset terminatedAt,
        CancellationToken cancellationToken = default)
    {
        return hubContext.Clients
            .User(userId.ToString())
            .SendAsync("sessionTerminated", new { reason, terminatedAt }, cancellationToken);
    }
}
