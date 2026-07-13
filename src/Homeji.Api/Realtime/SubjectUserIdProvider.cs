using Microsoft.AspNetCore.SignalR;

namespace Homeji.Api.Realtime;

public sealed class SubjectUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst("sub")?.Value;
    }
}
