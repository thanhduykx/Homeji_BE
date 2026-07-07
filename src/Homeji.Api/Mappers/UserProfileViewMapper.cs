using Homeji.Api.Views.Profiles;
using Homeji.Application.DTOs.Profiles;

namespace Homeji.Api.Mappers;

public static class UserProfileViewMapper
{
    public static UpdateMyProfileDto ToDto(UpdateMyProfileViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return new UpdateMyProfileDto(
            viewModel.DisplayName,
            viewModel.Phone,
            viewModel.AvatarPath,
            viewModel.School,
            viewModel.PreferredArea);
    }

    public static UpdateLifestyleDto ToDto(UpdateLifestyleViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return new UpdateLifestyleDto(
            viewModel.Role,
            viewModel.SleepHabit,
            viewModel.PetPreference,
            viewModel.SmokingPreference,
            viewModel.MaxBudget,
            viewModel.PreferredArea);
    }

    public static UserProfileViewModel ToViewModel(UserProfileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new UserProfileViewModel(
            dto.Id,
            dto.DisplayName,
            dto.Role,
            dto.Phone,
            dto.AvatarPath,
            dto.School,
            dto.PreferredArea,
            dto.SleepHabit,
            dto.PetPreference,
            dto.SmokingPreference,
            dto.MaxBudget,
            dto.OnboardingCompleted,
            dto.LandlordVerificationStatus,
            dto.CreatedAt,
            dto.UpdatedAt);
    }
}
