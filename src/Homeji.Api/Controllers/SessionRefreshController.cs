using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.IServices.Accounts;
using Homeji.Infrastructure.External;
using Homeji.Api.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/account/refresh")]
public sealed class SessionRefreshController(
    IHttpClientFactory clients,
    IOptions<SupaBaseAuthOptions> supabase,
    IOptionsMonitor<JwtBearerOptions> bearer,
    IUserSessionRevocationService revocations) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicAuth)]
    [HttpPost]
    public async Task<IActionResult> Refresh([FromBody] RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        // The signed original access token anchors the session before any rotation.
        // Ignoring its expiry is intentional; signature, issuer and audience still apply.
        var anchor = await ValidateAsync(request.SessionToken, allowExpired: true, cancellationToken);
        if (anchor is null || !Guid.TryParse(anchor.Subject, out var userId)
            || !anchor.TryGetPayloadValue<string>("session_id", out var sessionId)
            || !anchor.TryGetPayloadValue<long>("iat", out var issuedAt)) return Unauthorized();
        var revokedBefore = await revocations.GetRevokedBeforeAsync(userId, cancellationToken);
        if (revokedBefore.HasValue && DateTimeOffset.FromUnixTimeSeconds(issuedAt) <= revokedBefore.Value)
            return Unauthorized();

        var settings = supabase.Value;
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(settings.ProjectUrl.TrimEnd('/') + "/"), "auth/v1/token?grant_type=refresh_token"));
        message.Headers.Add("apikey", settings.ApiKey);
        message.Content = JsonContent.Create(new { refresh_token = request.RefreshToken });
        using var client = clients.CreateClient();
        using var result = await client.SendAsync(message, cancellationToken);
        if (result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return Unauthorized();
        if (!result.IsSuccessStatusCode) return StatusCode(StatusCodes.Status503ServiceUnavailable);
        using var json = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = json.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var nextRefresh = root.GetProperty("refresh_token").GetString();
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(nextRefresh))
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var renewed = await ValidateAsync(accessToken, allowExpired: false, cancellationToken);
        if (renewed is null || renewed.Subject != anchor.Subject
            || !renewed.TryGetPayloadValue<string>("session_id", out var renewedSession) || renewedSession != sessionId)
            return Unauthorized();
        // Recheck after the provider call so concurrent administrator revocation wins.
        revokedBefore = await revocations.GetRevokedBeforeAsync(userId, cancellationToken);
        if (revokedBefore.HasValue && DateTimeOffset.FromUnixTimeSeconds(issuedAt) <= revokedBefore.Value)
            return Unauthorized();
        return Ok(new { accessToken, refreshToken = nextRefresh, userId,
            email = root.GetProperty("user").GetProperty("email").GetString(),
            expiresIn = root.GetProperty("expires_in").GetInt32(), tokenType = "bearer",
            emailConfirmationRequired = false, message = "Đã gia hạn phiên đăng nhập." });
    }

    private async Task<JsonWebToken?> ValidateAsync(string token, bool allowExpired, CancellationToken cancellationToken)
    {
        var options = bearer.Get(JwtBearerDefaults.AuthenticationScheme);
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(cancellationToken);
        var parameters = options.TokenValidationParameters.Clone();
        parameters.IssuerSigningKeys = configuration.SigningKeys;
        parameters.ValidateLifetime = !allowExpired;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        return result.IsValid ? result.SecurityToken as JsonWebToken : null;
    }
}

public sealed record RefreshSessionRequest(
    [Required, StringLength(16384, MinimumLength = 20)] string SessionToken,
    [Required, StringLength(4096, MinimumLength = 8)] string RefreshToken);
