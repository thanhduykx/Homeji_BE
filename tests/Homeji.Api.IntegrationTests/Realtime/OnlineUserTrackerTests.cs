using Homeji.Api.Realtime;

namespace Homeji.Api.IntegrationTests.Realtime;

public sealed class OnlineUserTrackerTests
{
    [Fact]
    public void DisconnectingOneConnection_KeepsUserOnlineWhileAnotherConnectionRemains()
    {
        var tracker = new OnlineUserTracker();
        var userId = Guid.NewGuid();
        var firstConnectedAt = new DateTimeOffset(2026, 7, 23, 1, 0, 0, TimeSpan.Zero);
        var secondConnectedAt = firstConnectedAt.AddMinutes(1);

        tracker.Connected(userId, "first", firstConnectedAt);
        tracker.Connected(userId, "second", secondConnectedAt);
        tracker.Disconnected("first");

        var onlineUsers = tracker.GetOnlineUsers();

        Assert.Equal(secondConnectedAt, onlineUsers[userId]);
    }

    [Fact]
    public void DisconnectingLastConnection_RemovesUserFromOnlineUsers()
    {
        var tracker = new OnlineUserTracker();
        var userId = Guid.NewGuid();

        tracker.Connected(userId, "only", DateTimeOffset.UtcNow);
        tracker.Disconnected("only");

        Assert.DoesNotContain(userId, tracker.GetOnlineUsers().Keys);
    }
}
