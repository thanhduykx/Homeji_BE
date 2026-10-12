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

    [Theory]
    [InlineData(UserRole.Renter, "person")]
    [InlineData(UserRole.Admin, "unknown")]
    [InlineData(null, "unknown")]
    public void Roommate_person_or_unknown_price_never_combines_with_group_fees(UserRole? role, string basis)
    {
        var post = CreatePost(type: RentalPostType.RoommateShare);
        var result = RentalCostCalculator.Calculate(post, new(4, 100, 3, "kwh", "person", "month", 50_000, 0), role);
        Assert.Equal(basis, result.PriceBasis);
        Assert.Null(result.KnownMonthlySubtotal);
        Assert.Null(result.EstimatedMonthlyTotal);
        Assert.Null(result.InitialPayment);
        Assert.Contains(result.Unknown, message => message.Contains("không cộng"));
        Assert.Equal(3_000_000, post.Price);
    }

    [Fact]
    public void Landlord_roommate_price_remains_a_room_scenario()
    {
        var result = RentalCostCalculator.Calculate(CreatePost(type: RentalPostType.RoommateShare),
            new(2, 100, 3, "kwh", "person", "month", 50_000, 0), UserRole.Landlord);
        Assert.Equal("room", result.PriceBasis);
        Assert.Equal(3_750_000, result.EstimatedMonthlyTotal);
        Assert.Equal(6_000_000, result.InitialPayment);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Missing_rent_only_retains_known_positive_fee_subtotal(decimal rent)
    {
        var post = CreatePost(price: rent);
        var result = RentalCostCalculator.Calculate(post, new(2, 100, 3, "kwh", "person", "month", 50_000, 0));
        Assert.Equal(750_000, result.KnownMonthlySubtotal);
        Assert.Null(result.EstimatedMonthlyTotal);
        Assert.Null(result.InitialPayment);
        Assert.Contains(result.Unknown, message => message.StartsWith("Giá thuê", StringComparison.Ordinal));
    }

    [Fact]
    public void Missing_rent_and_confirmed_free_fees_do_not_fabricate_zero_total()
    {
        var result = RentalCostCalculator.Calculate(CreatePost(zeroFees: true, price: 0),
            new(OtherMonthlyFees: 0, OtherInitialFees: 0, ElectricityFreeConfirmed: true,
                WaterFreeConfirmed: true, InternetFreeConfirmed: true, DepositFreeConfirmed: true));
        Assert.Null(result.KnownMonthlySubtotal);
        Assert.Null(result.EstimatedMonthlyTotal);
        Assert.Null(result.InitialPayment);
    }

    private static RentalPost CreatePost(decimal latitude = 10.85m, bool zeroFees = false,
        RentalPostType type = RentalPostType.VacantRoom, decimal price = 3_000_000)
    {
        var now = new DateTimeOffset(2026, 10, 8, 0, 0, 0, TimeSpan.Zero);
        var post = RentalPost.CreateDraft(Guid.NewGuid(), type, now);
        post.UpdateDetails(type, "Phòng có bếp", "Không có máy lạnh. Không yên tĩnh.",
            3_000_000, zeroFees ? 0 : 3_000_000, 25, "Tăng Nhơn Phú, Thủ Đức", latitude, 106.80m,
            ["KITCHEN"], now, zeroFees ? 0 : 4000, zeroFees ? 0 : 100_000, zeroFees ? 0 : 100_000, 2, 2);
        for (var i = 0; i < 3; i++) post.AddMedia(MediaType.Image, "images", $"room/{i}.jpg", i == 0, i, now);
        post.Submit(now); post.Approve(now);
        // Simulate legacy/incomplete persisted data that current draft validation would reject.
        typeof(RentalPost).GetProperty(nameof(RentalPost.Price))!.SetValue(post, price);
        return post;
    }
}
