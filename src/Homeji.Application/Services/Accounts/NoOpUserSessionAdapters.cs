using Homeji.Application.Abstractions.Authentication;

namespace Homeji.Application.Services.Accounts;

public sealed class NoOpUserSessionRevocationCache : IUserSessionRevocationCache
{
    public bool TryGet(Guid userId, out DateTimeOffset? revokedBefore)
    {
        revokedBefore = null;
        return false;
    }

    public void Store(Guid userId, DateTimeOffset? revokedBefore)
    {
    }
}

public sealed class NoOpUserSessionRealtimePublisher : IUserSessionRealtimePublisher
{
    public Task TerminateAsync(
        Guid userId,
        string reason,
        DateTimeOffset terminatedAt,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
