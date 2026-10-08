using Homeji.Application.Services.AI;

namespace Homeji.Application.UnitTests.AI;

public sealed class RentalSearchIntentTests
{
    [Theory]
    [InlineData("Phòng từ 20 đến 30 m²", 20d, 30d)]
    [InlineData("phong tu 20m2 den 30m2", 20d, 30d)]
    [InlineData("Phòng ít nhất 25,5 mét vuông", 25.5, null)]
    [InlineData("Phòng dưới 30m2", null, 30d)]
    public void AreaConstraints_AreDeterministicWithoutModel(string text, double? minimum, double? maximum)
    {
        var result = RentalSearchIntent.Apply(text);
        Assert.Equal(minimum.HasValue ? (decimal?)minimum.Value : null, result.AreaMin);
        Assert.Equal(maximum.HasValue ? (decimal?)maximum.Value : null, result.AreaMax);
        Assert.Empty(result.Unknown);
    }

    [Fact]
    public void InvalidCapacityOrArea_RequiresClarificationAndCanBeCorrected()
    {
        var state = RentalSearchIntent.Apply("Phòng cho 200 người từ 30 đến 20m2 không muốn ở ghép");
        Assert.Contains("occupants", state.Unknown); Assert.Contains("area", state.Unknown);
        Assert.True(state.ExcludeRoommateShare);
        state = RentalSearchIntent.Apply("2 người từ 20 đến 30m2", state);
        Assert.Equal(2, state.Occupants); Assert.Empty(state.Unknown);
    }
    public static IEnumerable<object[]> VietnameseBudgetFixtures()
    {
        string[] prefixes = ["Tìm phòng dưới", "tim phong duoi", "Thuê trọ tối đa", "phòng không quá", "phong cao nhat"];
        string[] amounts = ["1tr", "2 triệu", "3tr", "4 triệu", "5tr", "1,5 triệu", "2.5tr", "3000000 VND", "4000000 đồng", "6 triệu"];
        decimal[] expected = [1_000_000, 2_000_000, 3_000_000, 4_000_000, 5_000_000, 1_500_000, 2_500_000, 3_000_000, 4_000_000, 6_000_000];
        foreach (var prefix in prefixes)
            for (var index = 0; index < amounts.Length; index++)
                yield return [prefix + " " + amounts[index] + " có bếp cho 2 người", expected[index]];
    }

    [Theory]
    [MemberData(nameof(VietnameseBudgetFixtures))]
    public void ExplicitVietnameseFixtures_PreserveCeilingOccupantsAndKitchen(string text, decimal ceiling)
    {
        var result = RentalSearchIntent.Apply(text);
        Assert.Equal(ceiling, result.PriceMax);
        Assert.Equal(2, result.Occupants);
        Assert.Contains("KITCHEN", result.RequiredAmenities);
        Assert.Empty(result.Unknown);
    }

    [Theory]
    [InlineData("Không cần máy lạnh")]
    [InlineData("khong can may lanh")]
    [InlineData("Bỏ máy lạnh")]
    public void RemovingRequirement_DoesNotKeepOrExcludeIt(string edit)
    {
        var initial = RentalSearchIntent.Apply("Phòng dưới 4tr có máy lạnh, có bếp");
        var updated = RentalSearchIntent.Apply(edit, initial);
        Assert.DoesNotContain("AIR_CONDITIONER", updated.RequiredAmenities);
        Assert.DoesNotContain("AIR_CONDITIONER", updated.ExcludedAmenities);
        Assert.Contains("KITCHEN", updated.RequiredAmenities);
        Assert.Equal(4_000_000, updated.PriceMax);
    }

    [Fact]
    public void TwentyEdits_OnlyKeepCurrentBudgetAndExplicitRequirements()
    {
        var state = RentalSearchIntent.Apply("Phòng dưới 4tr có bếp có máy lạnh");
        for (var index = 0; index < 20; index++)
        {
            state = RentalSearchIntent.Apply($"Phòng dưới {index % 3 + 1}tr " + (index % 2 == 0 ? "không cần máy lạnh" : "có máy lạnh"), state);
            Assert.Equal((index % 3 + 1) * 1_000_000m, state.PriceMax);
            Assert.Equal(index % 2 != 0, state.RequiredAmenities.Contains("AIR_CONDITIONER", StringComparer.Ordinal));
            Assert.Contains("KITCHEN", state.RequiredAmenities);
        }
    }

    [Fact]
    public void MissingFeesOrAmbiguousDestination_RequireClarification()
    {
        var state = RentalSearchIntent.Apply("2 người dưới 4tr cả phí gần FPT có bếp");
        Assert.Equal("total", state.BudgetBasis);
        Assert.Contains("feeUnits", state.Unknown);
        Assert.Contains("destination", state.Unknown);
        var clarified = RentalSearchIntent.Apply("Chỉ tiền thuê, FPT Khu Công nghệ cao", state);
        Assert.Equal("rent", clarified.BudgetBasis);
        Assert.DoesNotContain("feeUnits", clarified.Unknown);
        Assert.DoesNotContain("destination", clarified.Unknown);
    }

    [Fact]
    public void NegativeAmenitiesAndRoommateShare_AreExplicitExclusions()
    {
        var state = RentalSearchIntent.Apply("Phòng không ở ghép, không có máy lạnh, có bếp");
        Assert.True(state.ExcludeRoommateShare);
        Assert.Contains("AIR_CONDITIONER", state.ExcludedAmenities);
        Assert.DoesNotContain("AIR_CONDITIONER", state.RequiredAmenities);
    }

    [Fact]
    public void CommuteCeilingAndCampusAliases_AreUnconfirmedUntilRealRoutesExist()
    {
        var state = RentalSearchIntent.Apply("Phòng dưới 3.000.000đ, cho 2 người, tới SPKT dưới 20 phút bằng xe buýt");
        Assert.Equal(3_000_000, state.PriceMax); Assert.Equal(20, state.MaxCommuteMinutes);
        Assert.Equal("Sư phạm Kỹ thuật", state.Destination); Assert.Equal("TRANSIT", state.TravelMode);
        Assert.Contains("destination", state.Unknown); Assert.Contains("commute", state.Unknown);
        state = RentalSearchIntent.Apply("Bỏ điểm đến", state);
        Assert.Null(state.MaxCommuteMinutes); Assert.Null(state.Destination); Assert.Empty(state.Unknown);
    }

    [Theory]
    [InlineData("Phòng dưới 3000k", 3000000)]
    [InlineData("Phòng không quá 3,000,000 VND", 3000000)]
    [InlineData("Phòng từ 2000k đến 3tr", 3000000)]
    public void CurrencyVariants_DoNotLoseTheCeiling(string text, decimal ceiling)
    {
        Assert.Equal(ceiling, RentalSearchIntent.Apply(text).PriceMax);
    }

    [Fact]
    public void Motorcycle_IsNeverSilentlyInterpretedAsCar()
    {
        var state = RentalSearchIntent.Apply("Phòng gần FPT đi xe máy dưới 20 phút");
        Assert.Null(state.TravelMode); Assert.Contains("motorcycleCoverage", state.Unknown);
    }

    [Fact]
    public void RangeAndClearingBudget_PreserveCurrentMeaning()
    {
        var state = RentalSearchIntent.Apply("Phòng từ 2 đến 4tr");
        Assert.Equal(2_000_000, state.PriceMin); Assert.Equal(4_000_000, state.PriceMax);
        state = RentalSearchIntent.Apply("Rẻ hơn", state);
        state = RentalSearchIntent.Apply("Bỏ ngân sách", state);
        Assert.Null(state.PriceMin); Assert.Null(state.PriceMax); Assert.Empty(state.Unknown);
    }

    [Fact]
    public void CheaperWithoutAmount_DoesNotInventNewCeiling()
    {
        var state = RentalSearchIntent.Apply("Rẻ hơn", RentalSearchIntent.Apply("Phòng dưới 4tr"));
        Assert.Equal(4_000_000, state.PriceMax);
        Assert.Contains("budget", state.Unknown);
    }
}
