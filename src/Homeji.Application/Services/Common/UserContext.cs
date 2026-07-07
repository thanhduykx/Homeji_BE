using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Common;

public sealed class UserContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IUserProfileRepository _profiles;

    public UserContext(ICurrentUser currentUser, IUserProfileRepository profiles)
    {
        _currentUser = currentUser;
        _profiles = profiles;
    }

    public Guid GetRequiredUserId()
    {
        return _currentUser.UserId is { } userId && userId != Guid.Empty
            ? userId
            : throw new UnauthorizedAccessException("The authenticated token does not contain a valid subject.");
    }

    public async Task<UserProfile> GetRequiredProfileAsync(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        return await _profiles.GetByIdAsync(userId, cancellationToken)
            ?? throw new ForbiddenAccessException("Complete your profile before using this feature.");
    }

    public static void EnsureAdmin(UserProfile profile)
    {
        if (profile.Role != UserRole.Admin)
        {
            throw new ForbiddenAccessException("Admin role is required.");
        }
    }

    public static void EnsureOwner(Guid currentUserId, Guid ownerId)
    {
        if (currentUserId != ownerId)
        {
            throw new ForbiddenAccessException("You can only modify your own resource.");
        }
    }
}
