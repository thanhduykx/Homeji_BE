using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Admin;

public sealed record AdminActiveUserDto(
    Guid UserId,
    string DisplayName,
    UserRole Role,
    string? AvatarPath,
    DateTimeOffset LastSeenAt,
    bool IsOnline);
