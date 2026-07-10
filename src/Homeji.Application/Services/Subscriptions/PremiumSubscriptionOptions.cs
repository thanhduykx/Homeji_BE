namespace Homeji.Application.Services.Subscriptions;

public sealed class PremiumSubscriptionOptions
{
    public const string SectionName = "Subscriptions:Premium";

    public string Code { get; set; } = "PREMIUM_MONTHLY";

    public string Name { get; set; } = "Premium";

    public decimal Price { get; set; } = 99_000;

    public int DurationDays { get; set; } = 30;

    public int BoostMultiplier { get; set; } = 3;
}
