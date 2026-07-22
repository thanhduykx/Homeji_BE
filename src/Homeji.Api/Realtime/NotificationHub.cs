using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Homeji.Application.Abstractions.Presence;

namespace Homeji.Api.Realtime;

[Authorize]
public sealed class NotificationHub(IOnlineUserTracker onlineUsers) : Hub
{
    public override async Task OnConnectedAsync()
    {
        if (Guid.TryParse(Context.UserIdentifier, out var userId))
        {
            onlineUsers.Connected(userId, Context.ConnectionId, DateTimeOffset.UtcNow);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        onlineUsers.Disconnected(Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}
