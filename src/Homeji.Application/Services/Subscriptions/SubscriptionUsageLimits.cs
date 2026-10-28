namespace Homeji.Application.Services.Subscriptions;

public sealed record SubscriptionUsageLimits(int FollowedAreas, int ComparedPosts)
{
    public static SubscriptionUsageLimits Basic { get; } = new(10, 3);
    public static SubscriptionUsageLimits Premium { get; } = new(30, 5);
    public static SubscriptionUsageLimits For(bool premium) => premium ? Premium : Basic;
}
