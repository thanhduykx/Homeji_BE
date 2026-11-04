namespace Homeji.Application.IServices.Accounts;

/// <summary>The signed token that opened a browser session; its issue time is what administrator revocation compares.</summary>
public sealed record SessionAnchor(Guid UserId, string SessionId, DateTimeOffset IssuedAt, string? Email);

public enum SessionRotationStatus
{
    Rotated,
    Rejected,
    Unavailable,
}

public sealed record SessionRotation(
    SessionRotationStatus Status,
    string? AccessToken = null,
    string? RefreshToken = null,
    string? Email = null,
    int ExpiresIn = 0);

/// <summary>Validates session anchors and rotates provider refresh tokens within one session.</summary>
public interface ISessionRotator
{
    /// <summary>Accepts an expired token; signature, issuer, audience and revocation still apply.</summary>
    Task<SessionAnchor?> ValidateAnchorAsync(string? token, CancellationToken cancellationToken);

    /// <summary>Returns the seconds a token of this exact session has left, or null when it cannot be used.</summary>
    Task<int?> GetRemainingSecondsAsync(string? token, SessionAnchor anchor, CancellationToken cancellationToken);

    Task<SessionRotation> RotateAsync(SessionAnchor anchor, string refreshToken, CancellationToken cancellationToken);

    /// <summary>Best effort: revokes this session's refresh tokens at the provider.</summary>
    Task<bool> SignOutAsync(string accessToken, CancellationToken cancellationToken);
}
