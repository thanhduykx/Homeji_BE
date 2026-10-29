namespace Homeji.Application.DTOs.Profiles;

// Explicit allowlist: never expose contact details, lifestyle or financial preferences.
public sealed record PublicProfileDto(Guid Id, string DisplayName, string? AvatarPath,
    DateTimeOffset CreatedAt, bool IsPremium);
