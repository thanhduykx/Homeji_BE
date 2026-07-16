using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Marketplace;

public sealed class MarketplaceFinanceOptions
{
    public const string SectionName = "MarketplaceFinance";

    public decimal SellerReserve { get; set; } = WalletAccount.MinimumWithdrawalReserve;
    public decimal MinimumFoodPrice { get; set; } = 10_000m;
    public decimal MinimumFoodOrder { get; set; } = 25_000m;
    public int OrderRequestTimeoutMinutes { get; set; } = 30;
    // Intentionally empty: configuration binding appends collection entries instead of replacing
    // initialized defaults. The required appsettings section is validated at application startup.
    public List<MarketplaceSellerPlanOptions> SellerPlans { get; set; } = [];

    public MarketplaceSellerPlanOptions GetRequiredPlan(string? code)
    {
        var plan = SellerPlans.FirstOrDefault(candidate =>
            string.Equals(candidate.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));
        return plan ?? throw new ArgumentException("Seller package code is invalid.", nameof(code));
    }

    public MarketplaceSellerPlanOptions GetStarterPlan() =>
        SellerPlans.FirstOrDefault(plan => plan.MonthlyPrice == 0)
        ?? throw new InvalidOperationException("MarketplaceFinance must define a free starter plan.");

    public bool IsValid()
    {
        return SellerReserve >= WalletAccount.MinimumWithdrawalReserve
            && MinimumFoodPrice > 0
            && MinimumFoodOrder >= MinimumFoodPrice
            && OrderRequestTimeoutMinutes is >= 5 and <= 1_440
            && decimal.Truncate(SellerReserve) == SellerReserve
            && decimal.Truncate(MinimumFoodPrice) == MinimumFoodPrice
            && decimal.Truncate(MinimumFoodOrder) == MinimumFoodOrder
            && SellerPlans.Count >= 1
            && SellerPlans.Count(plan => plan.MonthlyPrice == 0) == 1
            && SellerPlans.Select(plan => plan.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count() == SellerPlans.Count
            && SellerPlans.All(plan =>
                !string.IsNullOrWhiteSpace(plan.Code)
                && !string.IsNullOrWhiteSpace(plan.Name)
                && plan.MonthlyPrice >= 0
                && decimal.Truncate(plan.MonthlyPrice) == plan.MonthlyPrice
                && plan.CommissionRate is > 0 and < 1
                && plan.DurationDays is >= 1 and <= 365);
    }
}

public sealed class MarketplaceSellerPlanOptions
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }
    public decimal CommissionRate { get; set; }
    public int DurationDays { get; set; } = 30;
}
