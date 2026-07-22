using System.Collections.Concurrent;
using Homeji.Application.Abstractions.Presence;

namespace Homeji.Api.Realtime;

public sealed class OnlineUserTracker : IOnlineUserTracker
{
    private readonly ConcurrentDictionary<string, OnlineConnection> _connections = new();

    public void Connected(Guid userId, string connectionId, DateTimeOffset connectedAt)
    {
        _connections[connectionId] = new OnlineConnection(userId, connectedAt);
    }

    public void Disconnected(string connectionId)
    {
        _connections.TryRemove(connectionId, out _);
    }

    public IReadOnlyDictionary<Guid, DateTimeOffset> GetOnlineUsers()
    {
        return _connections.Values
            .GroupBy(connection => connection.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(connection => connection.ConnectedAt));
    }

    private sealed record OnlineConnection(Guid UserId, DateTimeOffset ConnectedAt);
}
