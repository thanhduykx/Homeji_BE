using Homeji.Api.Mappers;
using Homeji.Api.RateLimiting;
using Homeji.Api.Views.Accounts;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.IServices.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController : ControllerBase
{
    private readonly IAccountService _accountService;

    public AccountController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpGet("email-availability")]
    [ProducesResponseType<EmailAvailabilityDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<EmailAvailabilityDto>> GetEmailAvailability(
        [FromQuery] string? email,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.GetEmailAvailabilityAsync(email, cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpPost("register")]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthSessionDto>> Register(
        [FromBody] RegisterAccountViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.RegisterAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpPost("login")]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthSessionDto>> Login(
        [FromBody] LoginAccountViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.LoginAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpPost("forgot-password")]
    [ProducesResponseType<AccountMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AccountMessageDto>> ForgotPassword(
        [FromBody] ForgotPasswordViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ForgotPasswordAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpPost("reset-password")]
    [ProducesResponseType<AccountMessageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AccountMessageDto>> ResetPassword(
        [FromBody] ResetPasswordViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ResetPasswordAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpGet("google/url")]
    [ProducesResponseType<AuthUrlDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public ActionResult<AuthUrlDto> GetGoogleLoginUrl([FromQuery] string? redirectTo)
    {
        return Ok(_accountService.CreateGoogleLoginUrl(redirectTo));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpGet("google/redirect")]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult RedirectToGoogle([FromQuery] string? redirectTo)
    {
        return Redirect(_accountService.CreateGoogleLoginUrl(redirectTo).Url);
    }
}
