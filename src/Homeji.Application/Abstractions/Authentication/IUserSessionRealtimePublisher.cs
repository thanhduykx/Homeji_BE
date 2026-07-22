namespace Homeji.Application.Abstractions.Authentication;

public interface IUserSessionRealtimePublisher
{
    Task TerminateAsync(
        Guid userId,
        string reason,
        DateTimeOffset terminatedAt,
        CancellationToken cancellationToken = default);
}
