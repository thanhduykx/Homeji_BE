using Homeji.Application.Profiles.Models;

namespace Homeji.Application.Profiles;

public interface IUserProfileService
{
    Task<UserProfileResponse> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<UserProfileResponse> UpsertMyProfileAsync(
        UpdateMyProfileRequest request,
        CancellationToken cancellationToken = default);
}
