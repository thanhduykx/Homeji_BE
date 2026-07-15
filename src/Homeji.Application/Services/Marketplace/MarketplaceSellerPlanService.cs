using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.Marketplace;

public sealed class MarketplaceSellerPlanService : IMarketplaceSellerPlanService
{
    private readonly UserContext _userContext;
    private readonly IMarketplaceSellerSubscriptionRepository _subscriptions;
    private readonly IWalletRepository _wallets;
    private readonly MarketplaceFinanceOptions _options;
    private readonly TimeProvider _timeProvider;

    public MarketplaceSellerPlanService(
        UserContext userContext,
        IMarketplaceSellerSubscriptionRepository subscriptions,
        IWalletRepository wallets,
        IOptions<MarketplaceFinanceOptions> options,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _subscriptions = subscriptions;
        _wallets = wallets;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<MarketplaceSellerPlanDto>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var active = await _subscriptions.GetActiveAsync(userId, _timeProvider.GetUtcNow(), cancellationToken);
        return _options.SellerPlans.Select(plan => new MarketplaceSellerPlanDto(
            plan.Code,
            plan.Name,
            plan.MonthlyPrice,
            plan.CommissionRate,
            plan.DurationDays,
            active is null ? plan.MonthlyPrice == 0 : active.PackageCode == plan.Code,
            active?.PackageCode == plan.Code ? active.ExpiresAt : null)).ToArray();
    }

    public async Task<MarketplaceSellerSubscriptionDto> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var active = await _subscriptions.GetActiveAsync(
            _userContext.GetRequiredUserId(),
            _timeProvider.GetUtcNow(),
            cancellationToken);
        return active is null ? ToStarterDto() : ToDto(active);
    }

    public async Task<MarketplaceSellerSubscriptionDto> PurchaseAsync(
        string packageCode,
        CancellationToken cancellationToken = default)
    {
        MarketplaceSellerPlanOptions plan;
        try
        {
            plan = _options.GetRequiredPlan(packageCode);
        }
        catch (ArgumentException)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["packageCode"] = ["Seller package code is invalid."],
            });
        }

        if (plan.MonthlyPrice <= 0)
        {
            return ToStarterDto();
        }

        var userId = _userContext.GetRequiredUserId();
        var wallet = await _wallets.GetAsync(userId, cancellationToken)
            ?? throw WalletFundingRequired(plan.MonthlyPrice + _options.SellerReserve);
        if (!wallet.IsActivated || wallet.Balance < plan.MonthlyPrice + _options.SellerReserve)
        {
            throw WalletFundingRequired(plan.MonthlyPrice + _options.SellerReserve - wallet.Balance);
        }

        var now = _timeProvider.GetUtcNow();
        var subscription = new MarketplaceSellerSubscription(
            userId,
            plan.Code,
            plan.Name,
            plan.MonthlyPrice,
            plan.CommissionRate,
            plan.DurationDays,
            now,
            now);
        wallet.DebitPurchase(plan.MonthlyPrice, now);
        await _subscriptions.AddAsync(subscription, cancellationToken);
        await _wallets.AddTransactionAsync(new WalletTransaction(
            userId,
            WalletTransactionKind.SellerPlanPurchase,
            -plan.MonthlyPrice,
            wallet.Balance,
            subscription.Id,
            $"Mua gói người bán {plan.Name}",
            now), cancellationToken);
        await _subscriptions.SaveChangesAsync(cancellationToken);
        return ToDto(subscription);
    }

    public async Task<decimal> ResolveCommissionRateAsync(
        Guid sellerId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var active = await _subscriptions.GetActiveAsync(sellerId, now, cancellationToken);
        return active?.CommissionRate ?? _options.GetStarterPlan().CommissionRate;
    }

    private MarketplaceSellerSubscriptionDto ToStarterDto()
    {
        var starter = _options.GetStarterPlan();
        return new MarketplaceSellerSubscriptionDto(
            starter.Code,
            starter.Name,
            0,
            starter.CommissionRate,
            _timeProvider.GetUtcNow(),
            null,
            false);
    }

    private static MarketplaceSellerSubscriptionDto ToDto(MarketplaceSellerSubscription subscription) => new(
        subscription.PackageCode,
        subscription.PackageName,
        subscription.Price,
        subscription.CommissionRate,
        subscription.StartsAt,
        subscription.ExpiresAt,
        true);

    private static RequestValidationException WalletFundingRequired(decimal missingAmount) =>
        new(new Dictionary<string, string[]>
        {
            ["wallet"] = [$"Nạp thêm ít nhất {Math.Max(0, missingAmount):0} đồng để mua gói và giữ quỹ đảm bảo bán hàng."],
        });
}
