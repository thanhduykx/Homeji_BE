using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Subscriptions;

public sealed record MySubscriptionDto(
    SubscriptionTier Tier,
    bool IsPremium,
    string Badge,
    string? PackageCode,
    string? PackageName,
    DateTimeOffset? PremiumStartedAt,
    DateTimeOffset? PremiumExpiresAt);
