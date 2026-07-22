namespace Homeji.Application.Abstractions.Presence;

public interface IOnlineUserTracker
{
    void Connected(Guid userId, string connectionId, DateTimeOffset connectedAt);
    void Disconnected(string connectionId);
    IReadOnlyDictionary<Guid, DateTimeOffset> GetOnlineUsers();
}
