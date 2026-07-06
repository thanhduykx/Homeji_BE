using Homeji.Application.DTOs.Profiles;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Profiles;

public static class UserProfileMapper
{
    public static UserProfileDto ToDto(UserProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new UserProfileDto(
            profile.Id,
            profile.DisplayName,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
