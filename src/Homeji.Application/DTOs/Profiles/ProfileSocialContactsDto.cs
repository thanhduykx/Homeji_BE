namespace Homeji.Application.DTOs.Profiles;

public sealed record UpdateSocialContactsDto(string? FacebookUrl, string? ZaloPhone);
public sealed record ProfileSocialContactsDto(string? FacebookUrl, string? ZaloPhone, DateTimeOffset CreatedAt, bool IsPremium, DateTimeOffset? PremiumExpiresAt);
