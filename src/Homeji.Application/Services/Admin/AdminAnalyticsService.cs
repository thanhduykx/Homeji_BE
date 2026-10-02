using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IServices.Admin;
using Homeji.Application.Services.Common;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Admin;

public sealed class AdminAnalyticsService : IAdminAnalyticsService
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly UserContext _userContext;
    private readonly IAdminAnalyticsRepository _analytics;
    private readonly TimeProvider _timeProvider;

    public AdminAnalyticsService(
        UserContext userContext,
        IAdminAnalyticsRepository analytics,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _analytics = analytics;
        _timeProvider = timeProvider;
    }

    public async Task<AdminProductAnalyticsDto> GetProductAnalyticsAsync(
        int periodDays,
        CancellationToken cancellationToken = default)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(profile);

        if (periodDays is < 7 or > 90)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["days"] = ["Khoảng phân tích phải từ 7 đến 90 ngày."],
            });
        }

        var now = _timeProvider.GetUtcNow();
        var localNow = now.ToOffset(VietnamOffset);
        var localFrom = new DateTimeOffset(
            localNow.Year,
            localNow.Month,
            localNow.Day,
            0,
            0,
            0,
            VietnamOffset).AddDays(-(periodDays - 1));
        var source = await _analytics.GetSnapshotAsync(localFrom.ToUniversalTime(), now, cancellationToken);

        return AdminAnalyticsCalculator.Calculate(source, localFrom, localNow, periodDays);
    }
}

public static class AdminAnalyticsCalculator
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static AdminProductAnalyticsDto Calculate(
        AdminAnalyticsSource source,
        DateTimeOffset localFrom,
        DateTimeOffset localNow,
        int periodDays)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (periodDays is < 1 or > 366)
        {
            throw new ArgumentOutOfRangeException(nameof(periodDays));
        }

        var activeRentals = source.Rentals
            .Where(rental => rental.Status == RentalPostStatus.Active)
            .ToArray();
        var prices = activeRentals
            .Where(rental => rental.Price > 0)
            .Select(rental => rental.Price)
            .ToArray();
        var pricePerSquareMeter = activeRentals
            .Where(rental => rental.Price > 0 && rental.Area > 0)
            .Select(rental => rental.Price / rental.Area)
            .ToArray();
        var searches = source.Activities.Count(activity => activity.Type == UserActivityType.RentalSearch);
        var listingViews = source.Activities.Count(activity => activity.Type == UserActivityType.ViewedRentalPost);
        var saves = source.Saves.Count;
        var viewingRequests = source.ViewingRequests.Count;

        var kpis = new AdminProductKpisDto(
            source.TotalUsers,
            source.NewUserDates.Count,
            activeRentals.Length,
            source.Rentals.Count(rental => rental.CreatedAt >= localFrom.ToUniversalTime()),
            Median(prices),
            Average(pricePerSquareMeter),
            searches,
            listingViews,
            saves,
            viewingRequests,
            Percentage(saves, listingViews),
            Percentage(viewingRequests, saves));

        var trend = BuildTrend(source, localFrom, periodDays);
        var areas = BuildAreas(source, activeRentals);

        return new AdminProductAnalyticsDto(
            localNow.ToUniversalTime(),
            periodDays,
            kpis,
            trend,
            areas);
    }

    private static AdminAnalyticsTrendPointDto[] BuildTrend(
        AdminAnalyticsSource source,
        DateTimeOffset localFrom,
        int periodDays)
    {
        var dates = Enumerable.Range(0, periodDays)
            .Select(offset => DateOnly.FromDateTime(localFrom.AddDays(offset).DateTime))
            .ToArray();

        var newUsers = CountByDate(source.NewUserDates);
        var newListings = CountByDate(source.Rentals.Select(rental => rental.CreatedAt));
        var searches = CountByDate(source.Activities
            .Where(activity => activity.Type == UserActivityType.RentalSearch)
            .Select(activity => activity.OccurredAt));
        var views = CountByDate(source.Activities
            .Where(activity => activity.Type == UserActivityType.ViewedRentalPost)
            .Select(activity => activity.OccurredAt));
        var saves = CountByDate(source.Saves.Select(saved => saved.CreatedAt));
        var viewingRequests = CountByDate(source.ViewingRequests.Select(request => request.CreatedAt));

        return dates.Select(date => new AdminAnalyticsTrendPointDto(
            date,
            GetCount(newUsers, date),
            GetCount(newListings, date),
            GetCount(searches, date),
            GetCount(views, date),
            GetCount(saves, date),
            GetCount(viewingRequests, date))).ToArray();
    }

    private static AdminAreaInsightDto[] BuildAreas(
        AdminAnalyticsSource source,
        AdminAnalyticsRentalRow[] activeRentals)
    {
        if (activeRentals.Length == 0) return [];

        var savesByRental = source.Saves
            .GroupBy(saved => saved.RentalPostId)
            .ToDictionary(group => group.Key, group => group.Count());
        var requestsByRental = source.ViewingRequests
            .GroupBy(request => request.RentalPostId)
            .ToDictionary(group => group.Key, group => group.Count());
        var viewsByRental = source.Activities
            .Where(activity => activity.Type == UserActivityType.ViewedRentalPost && activity.RelatedEntityId.HasValue)
            .GroupBy(activity => activity.RelatedEntityId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());
        var globalViews = activeRentals.Sum(rental => viewsByRental.GetValueOrDefault(rental.Id));
        var globalSaves = activeRentals.Sum(rental => savesByRental.GetValueOrDefault(rental.Id));
        var globalSaveRate = Ratio(globalSaves, globalViews);
        var globalRequestsPerListing = Ratio(
            activeRentals.Sum(rental => requestsByRental.GetValueOrDefault(rental.Id)), activeRentals.Length);
        var globalPricePerSquareMeter = Average(activeRentals
            .Where(rental => rental.Area > 0)
            .Select(rental => rental.Price / rental.Area));

        return activeRentals
            .Where(rental => rental.Latitude is >= -90 and <= 90
                && rental.Longitude is >= -180 and <= 180
                && (rental.Latitude != 0 || rental.Longitude != 0))
            .GroupBy(rental => ExtractAreaName(rental.Address), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var rentals = group.ToArray();
                var totalViews = rentals.Sum(rental => viewsByRental.GetValueOrDefault(rental.Id));
                var totalSaves = rentals.Sum(rental => savesByRental.GetValueOrDefault(rental.Id));
                var periodSaves = rentals.Sum(rental => savesByRental.GetValueOrDefault(rental.Id));
                var periodRequests = rentals.Sum(rental => requestsByRental.GetValueOrDefault(rental.Id));
                var saveRate = Ratio(totalSaves, totalViews);
                var requestsPerListing = Ratio(periodRequests, rentals.Length);
                var areaPricePerSquareMeter = Average(rentals
                    .Where(rental => rental.Area > 0)
                    .Select(rental => rental.Price / rental.Area));
                var demandIndex = DemandIndex(
                    saveRate,
                    globalSaveRate,
                    requestsPerListing,
                    globalRequestsPerListing,
                    periodSaves);
                // Sample gates are operational safeguards, not statistical confidence intervals.
                var confidence = rentals.Length >= 8 && totalViews >= 200 ? "high"
                    : rentals.Length >= 4 && totalViews >= 50 ? "medium" : "low";
                if (areaPricePerSquareMeter <= 0 || globalPricePerSquareMeter <= 0) confidence = "low";
                var (signal, recommendation) = PriceRecommendation(
                    demandIndex,
                    areaPricePerSquareMeter,
                    globalPricePerSquareMeter,
                    confidence);

                return new AdminAreaInsightDto(
                    group.Key,
                    rentals.Average(rental => (double)rental.Latitude),
                    rentals.Average(rental => (double)rental.Longitude),
                    rentals.Length,
                    Median(rentals.Select(rental => rental.Price)),
                    areaPricePerSquareMeter,
                    totalViews,
                    totalSaves,
                    periodRequests,
                    decimal.Round(saveRate * 100, 1),
                    demandIndex,
                    signal,
                    recommendation,
                    confidence);
            })
            .OrderByDescending(area => area.DemandIndex)
            .ThenByDescending(area => area.ActiveListings)
            .Take(20)
            .ToArray();
    }

    private static (string Signal, string Recommendation) PriceRecommendation(
        decimal demandIndex,
        decimal areaPricePerSquareMeter,
        decimal globalPricePerSquareMeter,
        string confidence)
    {
        if (confidence == "low")
        {
            return (
                "insufficient_data",
                "Chưa đủ mẫu tương tác hoặc dữ liệu giá để kết luận. Thu thập thêm lượt xem, lượt lưu và yêu cầu xem trước khi điều chỉnh giá.");
        }

        var priceRatio = globalPricePerSquareMeter > 0
            ? areaPricePerSquareMeter / globalPricePerSquareMeter
            : 1;
        if (demandIndex >= 125 && priceRatio <= 1.1m)
        {
            return (
                "test_increase",
                "Nhu cầu cao, giá/m² chưa vượt thị trường. Có thể thử tăng 3–5% trên một nhóm tin và đo lại chuyển đổi.");
        }

        if (demandIndex <= 75 && priceRatio >= 1.1m)
        {
            return (
                "review_decrease",
                "Giá/m² cao trong khi tương tác thấp. Thử giảm 3–5% hoặc cải thiện nội dung trước khi mở rộng.");
        }

        if (demandIndex >= 125)
        {
            return (
                "add_supply",
                "Nhu cầu cao nhưng giá đã cao. Ưu tiên bổ sung nguồn cung thay vì tăng giá đồng loạt.");
        }

        return (
            "hold",
            "Giữ giá hiện tại và tiếp tục theo dõi tỷ lệ lưu, lịch xem trước khi điều chỉnh.");
    }

    private static decimal DemandIndex(
        decimal saveRate,
        decimal globalSaveRate,
        decimal requestsPerListing,
        decimal globalRequestsPerListing,
        int periodSaves)
    {
        if (globalSaveRate <= 0 && globalRequestsPerListing <= 0)
        {
            return periodSaves > 0 ? 110 : 100;
        }

        var saveComponent = globalSaveRate > 0 ? saveRate / globalSaveRate : 1;
        var requestComponent = globalRequestsPerListing > 0
            ? requestsPerListing / globalRequestsPerListing
            : 1;
        return decimal.Round(Math.Clamp((saveComponent * 0.65m + requestComponent * 0.35m) * 100, 0, 250), 0);
    }

    private static Dictionary<DateOnly, int> CountByDate(IEnumerable<DateTimeOffset> dates) =>
        dates.GroupBy(date => DateOnly.FromDateTime(date.ToOffset(VietnamOffset).DateTime))
            .ToDictionary(group => group.Key, group => group.Count());

    private static int GetCount(Dictionary<DateOnly, int> counts, DateOnly date) =>
        counts.TryGetValue(date, out var count) ? count : 0;

    private static string ExtractAreaName(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "Khu vực chưa xác định";
        var parts = address.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var preferred = parts.FirstOrDefault(part =>
            part.StartsWith("phường ", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("xã ", StringComparison.OrdinalIgnoreCase)
            || part.StartsWith("thị trấn ", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(preferred)) return preferred;

        var district = parts.FirstOrDefault(part =>
            part.StartsWith("quận ", StringComparison.OrdinalIgnoreCase)
            || part.Contains("Thủ Đức", StringComparison.OrdinalIgnoreCase));
        return district ?? parts.Last();
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var sorted = values.Where(value => value >= 0).Order().ToArray();
        if (sorted.Length == 0) return 0;
        var middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? decimal.Round((sorted[middle - 1] + sorted[middle]) / 2, 0)
            : sorted[middle];
    }

    private static decimal Average(IEnumerable<decimal> values)
    {
        var data = values.Where(value => value >= 0).ToArray();
        return data.Length == 0 ? 0 : decimal.Round(data.Average(), 0);
    }

    private static decimal Ratio(int numerator, int denominator) =>
        denominator <= 0 ? 0 : (decimal)numerator / denominator;

    private static decimal Percentage(int numerator, int denominator) =>
        decimal.Round(Ratio(numerator, denominator) * 100, 1);
}
