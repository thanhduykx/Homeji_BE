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
            profile.Role,
            profile.Phone,
            profile.AvatarPath,
            profile.School,
            profile.PreferredArea,
            profile.SleepHabit,
            profile.PetPreference,
            profile.SmokingPreference,
            profile.MaxBudget,
            profile.OnboardingCompleted,
            profile.LandlordVerificationStatus,
            profile.CreatedAt,
            profile.UpdatedAt);
    }
}
