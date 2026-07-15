namespace Homeji.Application.DTOs.Marketplace;

public sealed record MarketplaceSellerPlanDto(
    string Code,
    string Name,
    decimal MonthlyPrice,
    decimal CommissionRate,
    int DurationDays,
    bool IsCurrent,
    DateTimeOffset? ExpiresAt);

public sealed record MarketplaceSellerSubscriptionDto(
    string PackageCode,
    string PackageName,
    decimal Price,
    decimal CommissionRate,
    DateTimeOffset StartsAt,
    DateTimeOffset? ExpiresAt,
    bool IsPaidPlan);
