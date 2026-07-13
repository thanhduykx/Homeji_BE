using Homeji.Application.DTOs.Payments;
using Homeji.Application.DTOs.Subscriptions;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.Payments;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.Services.Common;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.Subscriptions;

public sealed class SubscriptionService : ISubscriptionService
{
    private static readonly string[] BasicBenefits =
    [
        "Sử dụng đầy đủ chức năng web và app",
        "Tìm kiếm và xem bài đăng",
        "Lưu bài, báo cáo, gửi lời mời roommate",
    ];

    private static readonly string[] PremiumBenefits =
    [
        "Có badge Premium trên bài đăng",
        "Bài đăng được ưu tiên hiển thị theo điểm boost",
        "Tăng khả năng xuất hiện trong đề xuất AI",
    ];

    private readonly UserContext _userContext;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IPaymentService _payments;
    private readonly PremiumSubscriptionOptions _options;
    private readonly TimeProvider _timeProvider;

    public SubscriptionService(
        UserContext userContext,
        IUserSubscriptionRepository subscriptions,
        IPaymentService payments,
        IOptions<PremiumSubscriptionOptions> options,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _subscriptions = subscriptions;
        _payments = payments;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<SubscriptionPackageDto>> GetPackagesAsync(CancellationToken cancellationToken = default)
    {
        var packages = new List<SubscriptionPackageDto>
        {
            new(
                "BASIC",
                "Basic",
                SubscriptionTier.Basic,
                0,
                0,
                "Basic",
                BasicBenefits),
        };

        packages.AddRange(_options.GetPlans().Select(plan => new SubscriptionPackageDto(
            NormalizePackageCode(plan.Code),
            NormalizePackageName(plan.Name),
            SubscriptionTier.Premium,
            RequirePositivePrice(plan.Price),
            RequirePositiveDuration(plan.DurationDays),
            "Premium",
            PremiumBenefits)));

        return Task.FromResult<IReadOnlyList<SubscriptionPackageDto>>(packages);
    }

    public async Task<MySubscriptionDto> GetMySubscriptionAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var now = _timeProvider.GetUtcNow();
        var premium = await _subscriptions.GetActivePremiumAsync(userId, now, cancellationToken);

        return premium is null
            ? new MySubscriptionDto(
                SubscriptionTier.Basic,
                false,
                "Basic",
                null,
                null,
                null,
                null)
            : new MySubscriptionDto(
                SubscriptionTier.Premium,
                true,
                "Premium",
                premium.PackageCode,
                premium.PackageName,
                premium.StartedAt,
                premium.ExpiresAt);
    }

    public Task<MomoPaymentResponseDto> CreatePremiumMomoPaymentAsync(
        string packageCode,
        CancellationToken cancellationToken = default)
    {
        return _payments.CreatePremiumMomoPaymentAsync(packageCode, cancellationToken);
    }

    public Task<PayOsPaymentResponseDto> CreatePremiumPayOsPaymentAsync(
        string packageCode,
        CancellationToken cancellationToken = default)
    {
        return _payments.CreatePremiumPayOsPaymentAsync(packageCode, cancellationToken);
    }

    private static string NormalizePackageCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "PREMIUM_MONTHLY" : value.Trim();
    }

    private static string NormalizePackageName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Premium" : value.Trim();
    }

    private static decimal RequirePositivePrice(decimal price)
    {
        return price <= 0 ? 99_000 : price;
    }

    private static int RequirePositiveDuration(int durationDays)
    {
        return durationDays <= 0 ? 30 : durationDays;
    }
}
