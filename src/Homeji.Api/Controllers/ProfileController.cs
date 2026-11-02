using Homeji.Api.Mappers;
using Homeji.Api.Views.Profiles;
using Homeji.Application.IServices.Profiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/profile")]
public sealed class ProfileController : ControllerBase
{
    [Authorize]
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<Homeji.Application.DTOs.Profiles.PublicProfileDto>> GetPublicProfile(
        Guid userId, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await _profileService.GetPublicProfileAsync(userId, cancellationToken));
    }
    private readonly IUserProfileService _profileService;

    public ProfileController(IUserProfileService profileService)
    {
        _profileService = profileService;
    }

    [HttpGet("me/social-contacts")]
    public async Task<IActionResult> GetSocialContacts(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await _profileService.GetMySocialContactsAsync(cancellationToken));
    }

    [HttpPut("me/social-contacts")]
    public async Task<IActionResult> UpdateSocialContacts(Homeji.Application.DTOs.Profiles.UpdateSocialContactsDto request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await _profileService.UpdateMySocialContactsAsync(request, cancellationToken));
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

    [HttpPut("me/lifestyle")]
    [ProducesResponseType<UserProfileViewModel>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileViewModel>> UpdateMyLifestyle(
        [FromBody] UpdateLifestyleViewModel request,
        CancellationToken cancellationToken)
    {
        var profile = await _profileService.UpdateMyLifestyleAsync(
            UserProfileViewMapper.ToDto(request),
            cancellationToken);

        return Ok(UserProfileViewMapper.ToViewModel(profile));
    }
}
