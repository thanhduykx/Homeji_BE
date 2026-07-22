namespace Homeji.Application.IServices.Accounts;

public interface IUserSessionRevocationService
{
    Task<DateTimeOffset?> GetRevokedBeforeAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset> RevokeAsync(
        Guid userId,
        DateTimeOffset revokedBefore,
        CancellationToken cancellationToken = default);
}
