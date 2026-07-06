namespace Homeji.Application.Profiles.Models;

public sealed record UserProfileResponse(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
