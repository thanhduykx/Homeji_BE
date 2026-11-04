using System.ComponentModel.DataAnnotations;
using Homeji.Api.Authentication;
using Homeji.Api.RateLimiting;
using Homeji.Application.IServices.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

/// <summary>Browser session held in HttpOnly cookies. The refresh token never returns to JavaScript.</summary>
[ApiController]
[AllowAnonymous]
[Route("api/account/session")]
[EnableRateLimiting(RateLimitingPolicyNames.SessionCookie)]
public sealed class SessionCookieController(
    ISessionRotator rotator,
    SessionCookies cookies,
    ILogger<SessionCookieController> logger) : ControllerBase, IActionFilter
{
    /// <summary>A token this close to expiry is rotated instead of reused, matching the frontend's renewal margin.</summary>
    private const int MinimumReusableSeconds = 120;

    [NonAction]
    public void OnActionExecuting(ActionExecutingContext context)
    {
        Response.Headers.CacheControl = "no-store";
        if (!cookies.IsTrustedRequest(Request)) context.Result = StatusCode(StatusCodes.Status403Forbidden);
    }

    [NonAction]
    public void OnActionExecuted(ActionExecutedContext context)
    {
    }

    /// <summary>Returns a usable access token, calling the provider only when the cookie one cannot be reused.</summary>
    [HttpPost]
    public async Task<IActionResult> Current([FromBody] CurrentSessionRequest? request, CancellationToken cancellationToken)
    {
        var anchorToken = cookies.Anchor(Request);
        var anchor = await rotator.ValidateAnchorAsync(anchorToken, cancellationToken);
        if (anchor is null) return SignedOut();

        var accessToken = cookies.Access(Request);
        var remaining = await rotator.GetRemainingSecondsAsync(accessToken, anchor, cancellationToken);
        if (request?.ForceRotate != true && remaining >= MinimumReusableSeconds)
            return SessionBody(accessToken!, anchor, anchor.Email, remaining.Value);

        var refreshToken = cookies.Refresh(Request);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            // Sessions adopted without a refresh token stay usable until they expire.
            return remaining > 0 && request?.ForceRotate != true
                ? SessionBody(accessToken!, anchor, anchor.Email, remaining.Value)
                : SignedOut();
        }

        var rotation = await rotator.RotateAsync(anchor, refreshToken, cancellationToken);
        if (rotation.Status == SessionRotationStatus.Rejected) return SignedOut();
        if (rotation.Status == SessionRotationStatus.Unavailable) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (!cookies.TryWrite(Response, anchorToken!, rotation.AccessToken!, rotation.RefreshToken!))
        {
            SessionCookieLog.TokensTooLarge(logger, anchor.UserId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return SessionBody(rotation.AccessToken!, anchor, rotation.Email, rotation.ExpiresIn);
    }

    /// <summary>
    /// Moves tokens the browser already holds (old localStorage sessions, sign-in callback links)
    /// into cookies. It does not call the provider, so a deployment cannot cause a burst of rotations.
    /// </summary>
    [HttpPost("adopt")]
    public async Task<IActionResult> Adopt([FromBody] AdoptSessionRequest request, CancellationToken cancellationToken)
    {
        var anchor = await rotator.ValidateAnchorAsync(request.SessionToken, cancellationToken);
        if (anchor is null) return SignedOut();

        var accessToken = request.AccessToken ?? request.SessionToken;
        var remaining = await rotator.GetRemainingSecondsAsync(accessToken, anchor, cancellationToken);
        // An expired or foreign access token is not an error: the next request rotates from the refresh token.
        if (!cookies.TryWrite(Response, request.SessionToken, remaining is null ? request.SessionToken : accessToken, request.RefreshToken))
        {
            SessionCookieLog.TokensTooLarge(logger, anchor.UserId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        return NoContent();
    }

    [HttpDelete]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        var accessToken = cookies.Access(Request);
        cookies.Clear(Response);
        if (string.IsNullOrWhiteSpace(accessToken)) return NoContent();
        try
        {
            if (!await rotator.SignOutAsync(accessToken, cancellationToken))
                SessionCookieLog.ProviderSignOutFailed(logger, null);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            SessionCookieLog.ProviderSignOutFailed(logger, exception);
        }

        return NoContent();
    }

    private UnauthorizedResult SignedOut()
    {
        cookies.Clear(Response);
        return Unauthorized();
    }

    private OkObjectResult SessionBody(string accessToken, SessionAnchor anchor, string? email, int expiresIn) =>
        Ok(new
        {
            accessToken,
            refreshToken = (string?)null,
            userId = anchor.UserId,
            email,
            expiresIn,
            tokenType = "bearer",
            emailConfirmationRequired = false,
            message = "Đã khôi phục phiên đăng nhập.",
        });
}

public sealed record CurrentSessionRequest(bool ForceRotate = false);

public sealed record AdoptSessionRequest(
    [Required, StringLength(16384, MinimumLength = 20)] string SessionToken,
    [StringLength(16384, MinimumLength = 20)] string? AccessToken = null,
    [StringLength(4096, MinimumLength = 8)] string? RefreshToken = null);
