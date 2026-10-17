using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.IServices.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/account")]
[EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
public sealed class PasswordRecoveryController(IPasswordRecoveryOtpService recovery) : ControllerBase
{
    [HttpPost("request-password-otp")]
    public async Task<ActionResult<AccountMessageDto>> RequestOtp(RequestPasswordOtpDto request, CancellationToken cancellationToken) =>
        Ok(await recovery.RequestPasswordOtpAsync(request, cancellationToken));

    [HttpPost("reset-password-otp")]
    public async Task<ActionResult<AccountMessageDto>> ResetWithOtp(ResetPasswordOtpDto request, CancellationToken cancellationToken) =>
        Ok(await recovery.ResetPasswordWithOtpAsync(request, cancellationToken));
}
