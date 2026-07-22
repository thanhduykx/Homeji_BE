namespace Homeji.Application.Abstractions.Authentication;

public interface IUserSessionRevocationCache
{
    bool TryGet(Guid userId, out DateTimeOffset? revokedBefore);
    void Store(Guid userId, DateTimeOffset? revokedBefore);
}
