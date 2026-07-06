using Homeji.Application.DTOs.Profiles;

namespace Homeji.Application.IServices.Profiles;

public interface IUserProfileService
{
    Task<UserProfileDto> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpsertMyProfileAsync(
        UpdateMyProfileDto request,
        CancellationToken cancellationToken = default);
}
