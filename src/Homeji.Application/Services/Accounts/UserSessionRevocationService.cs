using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IServices.Accounts;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Accounts;

public sealed class UserSessionRevocationService(
    IUserProfileRepository profiles,
    IUserSessionRevocationCache cache) : IUserSessionRevocationService
{
    public async Task<DateTimeOffset?> GetRevokedBeforeAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (cache.TryGet(userId, out var cached)) return cached;

        var profile = await profiles.GetByIdAsync(userId, cancellationToken);
        var revokedBefore = profile?.SessionsRevokedBefore;
        cache.Store(userId, revokedBefore);
        return revokedBefore;
    }

    public async Task<DateTimeOffset> RevokeAsync(
        Guid userId,
        DateTimeOffset revokedBefore,
        CancellationToken cancellationToken = default)
    {
        var profile = await profiles.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(UserProfile), userId);
        profile.RevokeSessions(revokedBefore);
        await profiles.SaveAsync(profile, cancellationToken);
        cache.Store(userId, profile.SessionsRevokedBefore);
        return profile.SessionsRevokedBefore ?? revokedBefore;
    }
}
