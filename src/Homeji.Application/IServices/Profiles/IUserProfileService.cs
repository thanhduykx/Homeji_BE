using Homeji.Application.DTOs.Profiles;

namespace Homeji.Application.IServices.Profiles;

public interface IUserProfileService
{
    Task<ProfileSocialContactsDto> GetMySocialContactsAsync(CancellationToken cancellationToken = default);
    Task<ProfileSocialContactsDto> UpdateMySocialContactsAsync(UpdateSocialContactsDto request, CancellationToken cancellationToken = default);
    Task<PublicProfileDto> GetPublicProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> GetMyProfileAsync(CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpsertMyProfileAsync(
        UpdateMyProfileDto request,
        CancellationToken cancellationToken = default);

    Task<UserProfileDto> UpdateMyLifestyleAsync(
        UpdateLifestyleDto request,
        CancellationToken cancellationToken = default);
}
