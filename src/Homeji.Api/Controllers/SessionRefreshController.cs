using System.ComponentModel.DataAnnotations;
using Homeji.Api.Authentication;
using Homeji.Api.RateLimiting;
using Homeji.Application.IServices.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

/// <summary>Body-token renewal for browser tabs opened before the cookie session rollout.</summary>
[ApiController]
[Route("api/account/refresh")]
public sealed class SessionRefreshController(ISessionRotator rotator) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpPost]
    public async Task<IActionResult> Refresh([FromBody] RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        // The signed original access token anchors the session before any rotation.
        var anchor = await rotator.ValidateAnchorAsync(request.SessionToken, cancellationToken);
        if (anchor is null) return Unauthorized();

        var rotation = await rotator.RotateAsync(anchor, request.RefreshToken, cancellationToken);
        if (rotation.Status == SessionRotationStatus.Rejected) return Unauthorized();
        if (rotation.Status == SessionRotationStatus.Unavailable) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        return Ok(new { accessToken = rotation.AccessToken, refreshToken = rotation.RefreshToken, userId = anchor.UserId,
            email = rotation.Email, expiresIn = rotation.ExpiresIn, tokenType = "bearer",
            emailConfirmationRequired = false, message = "Đã gia hạn phiên đăng nhập." });
    }
}

public sealed record RefreshSessionRequest(
    [Required, StringLength(16384, MinimumLength = 20)] string SessionToken,
    [Required, StringLength(4096, MinimumLength = 8)] string RefreshToken);
