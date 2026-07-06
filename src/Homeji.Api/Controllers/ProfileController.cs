using Homeji.Api.Mappers;
using Homeji.Api.ViewModels.Profiles;
using Homeji.Application.IServices.Profiles;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/v1/profile")]
public sealed class ProfileController : ControllerBase
{
    private readonly IUserProfileService _profileService;

    public ProfileController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("me")]
    [ProducesResponseType<UserProfileViewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileViewModel>> GetMyProfile(
        CancellationToken cancellationToken)
    {
        var profile = await _profileService.GetMyProfileAsync(cancellationToken);
        return Ok(UserProfileViewMapper.ToViewModel(profile));
    }

    [HttpPut("me")]
    [ProducesResponseType<UserProfileViewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileViewModel>> UpsertMyProfile(
        [FromBody] UpdateMyProfileViewModel request,
        CancellationToken cancellationToken)
    {
        var profile = await _profileService.UpsertMyProfileAsync(
            UserProfileViewMapper.ToDto(request),
            cancellationToken);

        return Ok(UserProfileViewMapper.ToViewModel(profile));
    }
}
