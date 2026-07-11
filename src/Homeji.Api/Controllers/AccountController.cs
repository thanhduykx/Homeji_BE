using Homeji.Api.Mappers;
using Homeji.Api.Views.Accounts;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.IServices.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [HttpPost("check-email")]
    [ProducesResponseType<EmailAvailabilityDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EmailAvailabilityDto>> CheckEmail(
        [FromBody] CheckEmailViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.CheckEmailAsync(request.Email, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthSessionDto>> Register(
        [FromBody] RegisterAccountViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.RegisterAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthSessionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthSessionDto>> Login(
        [FromBody] LoginAccountViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.LoginAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    [ProducesResponseType<AccountMessageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountMessageDto>> ForgotPassword(
        [FromBody] ForgotPasswordViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ForgotPasswordAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("reset-password")]
    [ProducesResponseType<AccountMessageDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AccountMessageDto>> ResetPassword(
        [FromBody] ResetPasswordViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accountService.ResetPasswordAsync(AccountViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("google/url")]
    [ProducesResponseType<AuthUrlDto>(StatusCodes.Status200OK)]
    public ActionResult<AuthUrlDto> GetGoogleLoginUrl([FromQuery] string? redirectTo)
    {
        return Ok(_accountService.CreateGoogleLoginUrl(redirectTo));
    }

    [AllowAnonymous]
    [HttpGet("google/redirect")]
    public IActionResult RedirectToGoogle([FromQuery] string? redirectTo)
    {
        return Redirect(_accountService.CreateGoogleLoginUrl(redirectTo).Url);
    }
}
