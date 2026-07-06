namespace Homeji.Application.DTOs.Profiles;

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
