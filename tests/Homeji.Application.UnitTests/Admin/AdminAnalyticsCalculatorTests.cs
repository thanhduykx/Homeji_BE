using Homeji.Application.IRepositories.Admin;
using Homeji.Application.Services.Admin;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Admin;

public sealed class AdminAnalyticsCalculatorTests
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    [Fact]
    public void Calculate_BuildsTransparentPriceSignalsFromDemandAndPricePosition()
    {
        var localNow = new DateTimeOffset(2026, 10, 2, 12, 0, 0, VietnamOffset);
        var localFrom = new DateTimeOffset(2026, 9, 3, 0, 0, 0, VietnamOffset);
        var hotRentals = Enumerable.Range(0, 4)
            .Select(index => Rental(
                $"Đường D{index}, phường Long Thạnh Mỹ, TP. Thủ Đức",
                3_000_000,
                20,
                100,
                20,
                localNow.AddDays(-10)))
            .ToArray();
        var coldRentals = Enumerable.Range(0, 4)
            .Select(index => Rental(
                $"Đường Số {index}, phường Linh Trung, TP. Thủ Đức",
                6_000_000,
                20,
                100,
                2,
                localNow.AddDays(-8)))
            .ToArray();
        var hotRequests = hotRentals
            .SelectMany(rental => Enumerable.Range(0, 2)
                .Select(_ => new AdminAnalyticsAppointmentRow(
                    rental.Id,
                    ViewingAppointmentStatus.Pending,
                    localNow.AddDays(-2))))
            .ToArray();
        var hotSaves = hotRentals
            .SelectMany(rental => Enumerable.Range(0, 2)
                .Select(_ => new AdminAnalyticsSavedRow(rental.Id, localNow.AddDays(-3))))
            .ToArray();
        var source = new AdminAnalyticsSource(
            100,
            [.. hotRentals, .. coldRentals],
            [localNow.AddDays(-1)],
            [.. hotRentals.Concat(coldRentals).SelectMany(rental => Enumerable.Range(0, 25)
                .Select(_ => new AdminAnalyticsActivityRow(UserActivityType.ViewedRentalPost, rental.Id, localNow.AddDays(-4))))],
            hotSaves,
            hotRequests);

        var result = AdminAnalyticsCalculator.Calculate(source, localFrom, localNow, 30);

        var hot = Assert.Single(result.Areas, area => area.AreaName == "phường Long Thạnh Mỹ");
        var cold = Assert.Single(result.Areas, area => area.AreaName == "phường Linh Trung");
        Assert.Equal("test_increase", hot.PriceSignal);
        Assert.Equal("review_decrease", cold.PriceSignal);
        Assert.Equal("medium", hot.Confidence);
        Assert.True(hot.DemandIndex > cold.DemandIndex);
        Assert.Equal(4_500_000m, result.Kpis.MedianMonthlyPrice);
    }

    [Fact]
    public void Calculate_ReturnsCompleteDailyTrendUsingVietnamDates()
    {
        var localNow = new DateTimeOffset(2026, 10, 2, 9, 0, 0, VietnamOffset);
        var localFrom = new DateTimeOffset(2026, 9, 26, 0, 0, 0, VietnamOffset);
        var utcNearMidnight = new DateTimeOffset(2026, 10, 1, 18, 30, 0, TimeSpan.Zero);
        var source = new AdminAnalyticsSource(
            2,
            [Rental("phường Linh Trung, TP. Thủ Đức", 2_000_000, 20, 10, 1, utcNearMidnight)],
            [utcNearMidnight],
            [new AdminAnalyticsActivityRow(UserActivityType.RentalSearch, null, utcNearMidnight)],
            [],
            []);

        var result = AdminAnalyticsCalculator.Calculate(source, localFrom, localNow, 7);

        Assert.Equal(7, result.Trend.Count);
        var octoberSecond = Assert.Single(result.Trend, point => point.Date == new DateOnly(2026, 10, 2));
        Assert.Equal(1, octoberSecond.NewUsers);
        Assert.Equal(1, octoberSecond.NewListings);
        Assert.Equal(1, octoberSecond.Searches);
    }

    [Fact]
    public void Calculate_DoesNotRecommendPriceChangesFromLifetimeCountersWithoutPeriodViews()
    {
        var now = new DateTimeOffset(2026, 10, 2, 9, 0, 0, VietnamOffset);
        var rentals = Enumerable.Range(0, 8)
            .Select(_ => Rental("phường Linh Trung", 2_000_000, 20, 10_000, 1_000, now.AddDays(-100)))
            .ToArray();
        var result = AdminAnalyticsCalculator.Calculate(
            new AdminAnalyticsSource(10, rentals, [], [], [], []), now.AddDays(-6), now, 7);

        var area = Assert.Single(result.Areas);
        Assert.Equal("insufficient_data", area.PriceSignal);
        Assert.Equal("low", area.Confidence);
        Assert.Equal(0, area.TotalViews);
        Assert.Equal(0, area.TotalSaves);
    }

    [Fact]
    public void Calculate_ExcludesMissingCoordinatesAndInactiveRequestsFromAreaBaseline()
    {
        var now = new DateTimeOffset(2026, 10, 2, 9, 0, 0, VietnamOffset);
        var active = Rental("phường Linh Trung", 2_000_000, 20, 0, 0, now);
        var missing = Rental("phường Khác", 2_000_000, 20, 0, 0, now) with { Latitude = 0, Longitude = 0 };
        var inactive = active with { Id = Guid.NewGuid(), Status = RentalPostStatus.Archived };
        var source = new AdminAnalyticsSource(10, [active, missing, inactive], [], [], [],
            [new AdminAnalyticsAppointmentRow(inactive.Id, ViewingAppointmentStatus.Pending, now)]);

        var result = AdminAnalyticsCalculator.Calculate(source, now.AddDays(-6), now, 7);

        Assert.Equal(2, result.Kpis.ActiveListings);
        Assert.Equal(1, result.Kpis.ViewingRequests);
        var area = Assert.Single(result.Areas);
        Assert.Equal(0, area.ViewingRequests);
        Assert.Equal(100m, area.DemandIndex);
    }

    private static AdminAnalyticsRentalRow Rental(
        string address,
        decimal price,
        decimal area,
        int views,
        int saves,
        DateTimeOffset createdAt) =>
        new(
            Guid.NewGuid(),
            RentalPostStatus.Active,
            address,
            10.841m,
            106.810m,
            price,
            area,
            views,
            saves,
            createdAt);
}
