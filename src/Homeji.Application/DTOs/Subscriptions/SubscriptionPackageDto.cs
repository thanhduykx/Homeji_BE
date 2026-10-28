using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Subscriptions;

public sealed record SubscriptionPackageDto(
    string Code,
    string Name,
    SubscriptionTier Tier,
    decimal Price,
    int DurationDays,
    string Badge,
    IReadOnlyCollection<string> Benefits)
{
    public int MaxFollowedAreas => Homeji.Application.Services.Subscriptions.SubscriptionUsageLimits.For(Tier == SubscriptionTier.Premium).FollowedAreas;
    public int MaxComparedPosts => Homeji.Application.Services.Subscriptions.SubscriptionUsageLimits.For(Tier == SubscriptionTier.Premium).ComparedPosts;
}
