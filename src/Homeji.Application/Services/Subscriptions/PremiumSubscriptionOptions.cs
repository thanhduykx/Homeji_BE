namespace Homeji.Application.Services.Subscriptions;

public sealed class PremiumSubscriptionOptions
{
    public const string SectionName = "Subscriptions:Premium";

    public string Code { get; set; } = "PREMIUM_MONTHLY";

    public string Name { get; set; } = "Premium";

    public decimal Price { get; set; } = 99_000;

    public int DurationDays { get; set; } = 30;

    public int BoostMultiplier { get; set; } = 3;

    public List<PremiumPlanOptions> Plans { get; set; } = [];

    public DateTimeOffset? LaunchOfferStartsAt { get; set; }
    public DateTimeOffset? LaunchOfferEndsAt { get; set; }
    public bool IsLaunchOffer(DateTimeOffset now) => LaunchOfferStartsAt.HasValue && LaunchOfferEndsAt.HasValue
        && now >= LaunchOfferStartsAt.Value && now < LaunchOfferEndsAt.Value;

    public IReadOnlyList<PremiumPlanOptions> GetPlansAt(DateTimeOffset now) => GetPlans().Select(plan =>
        IsLaunchOffer(now) && plan.Code == "PREMIUM_MONTHLY" && plan.Price == 99_000
            ? new PremiumPlanOptions { Code = plan.Code, Name = plan.Name, Price = 49_000, DurationDays = plan.DurationDays, OriginalPrice = 99_000, OfferEndsAt = LaunchOfferEndsAt }
            : plan).ToArray();

    public IReadOnlyList<PremiumPlanOptions> GetPlans()
    {
        return Plans.Count > 0
            ? Plans
            : [new PremiumPlanOptions { Code = Code, Name = Name, Price = Price, DurationDays = DurationDays }];
    }
}

public sealed class PremiumPlanOptions
{
    public decimal? OriginalPrice { get; set; }
    public DateTimeOffset? OfferEndsAt { get; set; }
    public bool OfferUsed { get; set; }
    public string Code { get; set; } = "PREMIUM_MONTHLY";
    public string Name { get; set; } = "Gói Trải Nghiệm";
    public decimal Price { get; set; } = 99_000;
    public int DurationDays { get; set; } = 30;
}
