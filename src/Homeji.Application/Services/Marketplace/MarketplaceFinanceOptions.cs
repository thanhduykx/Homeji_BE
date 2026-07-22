using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Marketplace;

public sealed class MarketplaceFinanceOptions
{
    public const string SectionName = "MarketplaceFinance";

    public decimal SellerReserve { get; set; } = WalletAccount.MinimumWithdrawalReserve;
    public decimal CommissionRate { get; set; } = 0.10m;
    public decimal MinimumFoodPrice { get; set; } = 10_000m;
    public decimal MinimumFoodOrder { get; set; } = 25_000m;
    public int OrderRequestTimeoutMinutes { get; set; } = 30;
    public bool IsValid()
    {
        return SellerReserve >= WalletAccount.MinimumWithdrawalReserve
            && CommissionRate is > 0 and < 1
            && MinimumFoodPrice > 0
            && MinimumFoodOrder >= MinimumFoodPrice
            && OrderRequestTimeoutMinutes is >= 5 and <= 1_440
            && decimal.Truncate(SellerReserve) == SellerReserve
            && decimal.Truncate(MinimumFoodPrice) == MinimumFoodPrice
            && decimal.Truncate(MinimumFoodOrder) == MinimumFoodOrder;
    }
}
