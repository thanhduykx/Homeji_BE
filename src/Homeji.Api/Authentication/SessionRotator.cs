using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.IServices.Accounts;
using Homeji.Infrastructure.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Homeji.Api.Authentication;

public sealed class SessionRotator(
    IHttpClientFactory clients,
    IOptions<SupaBaseAuthOptions> supabase,
    IOptionsMonitor<JwtBearerOptions> bearer,
    IUserSessionRevocationService revocations) : ISessionRotator
{
    public async Task<SessionAnchor?> ValidateAnchorAsync(string? token, CancellationToken cancellationToken)
    {
        var anchor = await ValidateAsync(token, allowExpired: true, cancellationToken);
        if (anchor is null || !Guid.TryParse(anchor.Subject, out var userId)
            || !anchor.TryGetPayloadValue<string>("session_id", out var sessionId)
            || !anchor.TryGetPayloadValue<long>("iat", out var issuedAt)) return null;
        anchor.TryGetPayloadValue<string>("email", out var email);
        var session = new SessionAnchor(userId, sessionId, DateTimeOffset.FromUnixTimeSeconds(issuedAt), email);
        return await IsRevokedAsync(session, cancellationToken) ? null : session;
    }

    public async Task<int?> GetRemainingSecondsAsync(string? token, SessionAnchor anchor, CancellationToken cancellationToken)
    {
        var access = await ValidateAsync(token, allowExpired: false, cancellationToken);
        if (access is null || !BelongsTo(access, anchor)) return null;
        return (int)Math.Max(0, (access.ValidTo - DateTime.UtcNow).TotalSeconds);
    }

    public async Task<SessionRotation> RotateAsync(SessionAnchor anchor, string refreshToken, CancellationToken cancellationToken)
    {
        var settings = supabase.Value;
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(settings.ProjectUrl.TrimEnd('/') + "/"), "auth/v1/token?grant_type=refresh_token"));
        message.Headers.Add("apikey", settings.ApiKey);
        message.Content = JsonContent.Create(new { refresh_token = refreshToken });
        using var client = clients.CreateClient();
        using var result = await client.SendAsync(message, cancellationToken);
        if (result.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new SessionRotation(SessionRotationStatus.Rejected);
        if (!result.IsSuccessStatusCode) return new SessionRotation(SessionRotationStatus.Unavailable);
        using var json = await JsonDocument.ParseAsync(await result.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var root = json.RootElement;
        var accessToken = root.GetProperty("access_token").GetString();
        var nextRefresh = root.GetProperty("refresh_token").GetString();
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(nextRefresh))
            return new SessionRotation(SessionRotationStatus.Unavailable);
        var renewed = await ValidateAsync(accessToken, allowExpired: false, cancellationToken);
        if (renewed is null || !BelongsTo(renewed, anchor)) return new SessionRotation(SessionRotationStatus.Rejected);
        // Recheck after the provider call so concurrent administrator revocation wins.
        if (await IsRevokedAsync(anchor, cancellationToken)) return new SessionRotation(SessionRotationStatus.Rejected);
        return new SessionRotation(SessionRotationStatus.Rotated, accessToken, nextRefresh,
            root.GetProperty("user").GetProperty("email").GetString(), root.GetProperty("expires_in").GetInt32());
    }

    public async Task<bool> SignOutAsync(string accessToken, CancellationToken cancellationToken)
    {
        var settings = supabase.Value;
        using var message = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(settings.ProjectUrl.TrimEnd('/') + "/"), "auth/v1/logout?scope=local"));
        message.Headers.Add("apikey", settings.ApiKey);
        message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var client = clients.CreateClient();
        using var result = await client.SendAsync(message, timeout.Token);
        return result.IsSuccessStatusCode;
    }

    private static bool BelongsTo(JsonWebToken token, SessionAnchor anchor) =>
        Guid.TryParse(token.Subject, out var userId) && userId == anchor.UserId
        && token.TryGetPayloadValue<string>("session_id", out var sessionId) && sessionId == anchor.SessionId;

    private async Task<bool> IsRevokedAsync(SessionAnchor anchor, CancellationToken cancellationToken)
    {
        var revokedBefore = await revocations.GetRevokedBeforeAsync(anchor.UserId, cancellationToken);
        return revokedBefore.HasValue && anchor.IssuedAt <= revokedBefore.Value;
    }

    private async Task<JsonWebToken?> ValidateAsync(string? token, bool allowExpired, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var options = bearer.Get(JwtBearerDefaults.AuthenticationScheme);
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(cancellationToken);
        var parameters = options.TokenValidationParameters.Clone();
        parameters.IssuerSigningKeys = configuration.SigningKeys;
        parameters.ValidateLifetime = !allowExpired;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        return result.IsValid ? result.SecurityToken as JsonWebToken : null;
    }
}
