using Homeji.Api.ViewModels.Profiles;
using Homeji.Application.DTOs.Profiles;

namespace Homeji.Api.Mappers;

public static class UserProfileViewMapper
{
    public static UpdateMyProfileDto ToDto(UpdateMyProfileViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return new UpdateMyProfileDto(viewModel.DisplayName);
    }

    public static UserProfileViewModel ToViewModel(UserProfileDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new UserProfileViewModel(
            dto.Id,
            dto.DisplayName,
            dto.CreatedAt,
            dto.UpdatedAt);
    }
}
