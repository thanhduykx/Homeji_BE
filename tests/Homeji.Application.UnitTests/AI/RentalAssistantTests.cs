using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.Services.AI;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.AI;

public sealed class RentalAssistantTests
{
    // Search coverage uses the current main contracts in AiSearchGroundingTests and RentalSearchIntentTests.

    [Fact]
    public void CostCalculationRespectsUnitsAndSeparatesDeposit()
    {
        var post = CreatePost();
        var scenario = new RentalCostScenarioDto(2, 100, 3, "kwh", "person", "month", 50_000, 0);
        var cost = RentalCostCalculator.Calculate(post, scenario);
        Assert.Equal(3_750_000, cost.KnownMonthlySubtotal);
        Assert.Equal(3_750_000, cost.EstimatedMonthlyTotal);
        Assert.Equal(6_000_000, cost.InitialPayment);
        Assert.Empty(cost.Unknown);
    }

    [Fact]
    public void ZeroIsUnknownUntilExplicitlyConfirmedInUserScenario()
    {
        var post = CreatePost(zeroFees: true);
        var unknown = RentalCostCalculator.Calculate(post, new());
        Assert.Null(unknown.EstimatedMonthlyTotal);
        Assert.Null(unknown.InitialPayment);
        Assert.Equal(4, unknown.Unknown.Count);
        var confirmed = RentalCostCalculator.Calculate(post, new(OtherMonthlyFees: 0, OtherInitialFees: 0,
            ElectricityFreeConfirmed: true, WaterFreeConfirmed: true, InternetFreeConfirmed: true, DepositFreeConfirmed: true));
        Assert.Equal(post.Price, confirmed.EstimatedMonthlyTotal);
        Assert.Equal(post.Price, confirmed.InitialPayment);
        Assert.Equal("ownerListing + userScenario", confirmed.SourceType);
    }

    [Theory]
    [InlineData("m3", "person", 1)]
    [InlineData("kwh", "kwh", 1)]
    [InlineData("kwh", "m3", 0)]
    public void InvalidUnitsAndPeopleAreRejected(string electricity, string water, int occupants)
        => Assert.Throws<RequestValidationException>(() => RentalCostCalculator.Calculate(CreatePost(), new(occupants, ElectricityUnit: electricity, WaterUnit: water)));

    private static RentalPost CreatePost(decimal latitude = 10.85m, bool zeroFees = false)
    {
        var now = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.VacantRoom, now);
        post.UpdateDetails(RentalPostType.VacantRoom, "Phòng có bếp", "Không có máy lạnh. Không yên tĩnh.",
            3_000_000, zeroFees ? 0 : 3_000_000, 25, "Tăng Nhơn Phú, Thủ Đức", latitude, 106.80m,
            ["KITCHEN"], now, zeroFees ? 0 : 4000, zeroFees ? 0 : 100_000, zeroFees ? 0 : 100_000, 2, 2);
        for (var i = 0; i < 3; i++) post.AddMedia(MediaType.Image, "images", $"room/{i}.jpg", i == 0, i, now);
        post.Submit(now); post.Approve(now);
        return post;
    }
}
