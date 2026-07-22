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

    public Guid? TryGetUserId()
    {
        return _currentUser.UserId is { } userId && userId != Guid.Empty ? userId : null;
    }

    public Guid GetRequiredUserId()
    {
        return TryGetUserId()
            ?? throw new UnauthorizedAccessException("Token đăng nhập không chứa thông tin người dùng hợp lệ.");
    }

    public async Task<UserProfile> GetRequiredProfileAsync(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        return await _profiles.GetByIdAsync(userId, cancellationToken)
            ?? throw new ForbiddenAccessException("Vui lòng hoàn thiện hồ sơ trước khi dùng tính năng này.");
    }

    public static void EnsureAdmin(UserProfile profile)
    {
        if (profile.Role != UserRole.Admin)
        {
            throw new ForbiddenAccessException("Cần quyền quản trị viên.");
        }
    }

    public static void EnsureRenter(UserProfile profile)
    {
        EnsureRole(profile, UserRole.Renter, "Cần vai trò người thuê.");
    }

    public static void EnsureLandlord(UserProfile profile)
    {
        EnsureRole(profile, UserRole.Landlord, "Cần vai trò chủ trọ.");
    }

    public static void EnsureOwner(Guid currentUserId, Guid ownerId)
    {
        if (currentUserId != ownerId)
        {
            throw new ForbiddenAccessException("Bạn chỉ có thể sửa tài nguyên của chính mình.");
        }
    }

    private static void EnsureRole(UserProfile profile, UserRole role, string message)
    {
        if (profile.Role != role)
        {
            throw new ForbiddenAccessException(message);
        }
    }
}
