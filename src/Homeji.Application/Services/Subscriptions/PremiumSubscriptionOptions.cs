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

    public IReadOnlyList<PremiumPlanOptions> GetPlans()
    {
        return Plans.Count > 0
            ? Plans
            : [new PremiumPlanOptions { Code = Code, Name = Name, Price = Price, DurationDays = DurationDays }];
    }
}

public sealed class PremiumPlanOptions
{
    public string Code { get; set; } = "PREMIUM_MONTHLY";
    public string Name { get; set; } = "Premium Monthly";
    public decimal Price { get; set; } = 99_000;
    public int DurationDays { get; set; } = 30;
}
