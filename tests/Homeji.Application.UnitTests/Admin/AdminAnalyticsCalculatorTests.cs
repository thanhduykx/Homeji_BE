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

        var hot = Assert.Single(result.Areas, area => area.AreaName == "phường Long Thạnh Mỹ, TP. Thủ Đức");
        var cold = Assert.Single(result.Areas, area => area.AreaName == "phường Linh Trung, TP. Thủ Đức");
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

    [Fact]
    public void Calculate_DoesNotMergeSameWardNameInDifferentCities()
    {
        var now = new DateTimeOffset(2026, 10, 3, 9, 0, 0, VietnamOffset);
        var first = Rental("12 Đường A, phường Bình An, TP. Thủ Đức", 2_000_000, 20, 0, 0, now);
        var second = Rental("18 Đường B, phường Bình An, TP. Dĩ An", 4_000_000, 20, 0, 0, now);
        var source = new AdminAnalyticsSource(0, [first, second], [], [], [], []);

        var result = AdminAnalyticsCalculator.Calculate(source, now.AddDays(-6), now, 7);

        Assert.Equal(2, result.Areas.Count);
        Assert.Equal(2_000_000m, Assert.Single(result.Areas,
            area => area.AreaName == "phường Bình An, TP. Thủ Đức").MedianMonthlyPrice);
        Assert.Equal(4_000_000m, Assert.Single(result.Areas,
            area => area.AreaName == "phường Bình An, TP. Dĩ An").MedianMonthlyPrice);
    }

    [Fact]
    public void Mixed_units_missing_prices_and_missing_area_never_enter_room_price_statistics()
    {
        var now = new DateTimeOffset(2026, 10, 12, 9, 0, 0, VietnamOffset);
        var room = Rental("phường Linh Trung", 3_000_000, 20, 10, 2, now);
        var noArea = room with { Id = Guid.NewGuid(), Price = 5_000_000, Area = 0 };
        var person = room with { Id = Guid.NewGuid(), Price = 500_000, PriceBasis = "person_month" };
        var unknown = room with { Id = Guid.NewGuid(), Price = 100_000, PriceBasis = "unknown" };
        var missing = room with { Id = Guid.NewGuid(), Price = 0 };
        var inactive = room with { Id = Guid.NewGuid(), Price = 100_000_000, Status = RentalPostStatus.Archived };
        var source = new AdminAnalyticsSource(10, [room, noArea, person, unknown, missing, inactive], [],
            [new(UserActivityType.ViewedRentalPost, person.Id, now)], [new(unknown.Id, now)], [new(missing.Id, ViewingAppointmentStatus.Pending, now)]);
        var result = AdminAnalyticsCalculator.Calculate(source, now.AddDays(-6), now, 7);
        Assert.Equal(5, result.Kpis.ActiveListings);
        Assert.Equal(4_000_000, result.Kpis.MedianMonthlyPrice);
        Assert.Equal(150_000, result.Kpis.AveragePricePerSquareMeter);
        Assert.Equal(2, result.Kpis.PriceSampleCount);
        Assert.Equal(3, result.Kpis.ExcludedPriceCount);
        Assert.Equal(1, result.Kpis.PriceAreaSampleCount);
        Assert.Equal("room_month", result.Kpis.PriceBasis);
        var area = Assert.Single(result.Areas);
        Assert.Equal(5, area.ActiveListings);
        Assert.Equal(4_000_000, area.MedianMonthlyPrice);
        Assert.Equal(150_000, area.AveragePricePerSquareMeter);
        Assert.Equal(2, area.PriceSampleCount);
        Assert.Equal(3, area.ExcludedPriceCount);
        Assert.Equal(1, area.PriceAreaSampleCount);
        Assert.Equal(1, area.TotalViews);
        Assert.Equal(1, area.TotalSaves);
        Assert.Equal(1, area.ViewingRequests);
    }

    [Theory]
    [InlineData("unknown", 3_000_000)]
    [InlineData("person_month", 1_000_000)]
    [InlineData("room_month", 0)]
    [InlineData("room_month", -1)]
    [InlineData(null, 3_000_000)]
    public void Missing_eligible_prices_are_disclosed_and_never_recommended(string? basis, decimal price)
    {
        var now = new DateTimeOffset(2026, 10, 12, 9, 0, 0, VietnamOffset);
        var rows = Enumerable.Range(0, 8).Select(_ => Rental("phường Linh Trung", price, 20, 0, 0, now) with { PriceBasis = basis! }).ToArray();
        var views = rows.SelectMany(row => Enumerable.Range(0, 30).Select(_ => new AdminAnalyticsActivityRow(UserActivityType.ViewedRentalPost, row.Id, now))).ToArray();
        var result = AdminAnalyticsCalculator.Calculate(new(10, rows, [], views, [], []), now.AddDays(-6), now, 7);
        Assert.Equal(0, result.Kpis.PriceSampleCount);
        Assert.Equal(8, result.Kpis.ExcludedPriceCount);
        Assert.Equal(0, result.Kpis.MedianMonthlyPrice);
        Assert.Equal(0, result.Kpis.AveragePricePerSquareMeter);
        var area = Assert.Single(result.Areas);
        Assert.Equal(240, area.TotalViews);
        Assert.Equal("insufficient_data", area.PriceSignal);
        Assert.Equal("low", area.Confidence);
    }

    [Fact]
    public void Many_active_listings_and_views_cannot_replace_a_missing_price_area_sample()
    {
        var now = new DateTimeOffset(2026, 10, 12, 9, 0, 0, VietnamOffset);
        var rows = Enumerable.Range(0, 8).Select(index => Rental("phường Linh Trung", 3_000_000, index == 0 ? 20 : 0, 0, 0, now)).ToArray();
        var views = rows.SelectMany(row => Enumerable.Range(0, 30).Select(_ => new AdminAnalyticsActivityRow(UserActivityType.ViewedRentalPost, row.Id, now))).ToArray();
        var result = AdminAnalyticsCalculator.Calculate(new(10, rows, [], views, [], []), now.AddDays(-6), now, 7);
        Assert.Equal(8, result.Kpis.PriceSampleCount);
        Assert.Equal(1, result.Kpis.PriceAreaSampleCount);
        var area = Assert.Single(result.Areas);
        Assert.Equal("insufficient_data", area.PriceSignal);
        Assert.Equal("low", area.Confidence);
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
            createdAt,
            PriceBasis: "room_month");
}
