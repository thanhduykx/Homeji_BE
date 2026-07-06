namespace Homeji.Application.DTOs.Profiles;

public sealed record UpdateMyProfileDto(
    string? DisplayName,
    string? Phone = null,
    string? AvatarPath = null,
    string? School = null,
    string? PreferredArea = null);
