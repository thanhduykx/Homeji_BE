using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.Services.AI;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.AI;

public sealed class RentalAssistantTests
{
    public static IEnumerable<object?[]> VietnameseQueries()
    {
        (string Text, decimal? Budget, int? People, string? Required, string? Destination)[] fixtures =
        [
            ("Phòng dưới 3tr", 3_000_000, null, null, null),
            ("Trọ tối đa 4 triệu", 4_000_000, null, null, null),
            ("Giá không quá 2,5 triệu", 2_500_000, null, null, null),
            ("Phòng 3.5tr", 3_500_000, null, null, null),
            ("Ngân sách 4000000 đồng", 4_000_000, null, null, null),
            ("Tiền thuê 4.000.000 VND", 4_000_000, null, null, null),
            ("Thuê dưới 1,8tr cho 2 người", 1_800_000, 2, null, null),
            ("3 người dưới 5tr có bếp", 5_000_000, 3, "KITCHEN", null),
            ("2 người cả phí tối đa 4 triệu gần FPT có bếp", 4_000_000, 2, "KITCHEN", "fpt"),
            ("1 người dưới 3tr có máy lạnh", 3_000_000, 1, "AIR_CONDITIONER", null),
            ("Cần phòng có điều hòa", null, null, "AIR_CONDITIONER", null),
            ("Phòng có wifi", null, null, "WIFI", null),
            ("Chỗ nấu ăn cho 2 người", null, 2, "KITCHEN", null),
            ("Cần WC riêng", null, null, "PRIVATE_TOILET", null),
            ("Cần vệ sinh riêng", null, null, "PRIVATE_TOILET", null),
            ("Bắt buộc có bãi xe", null, null, "PARKING", null),
            ("Cần giữ xe", null, null, "PARKING", null),
            ("Có giờ giấc tự do", null, null, "FREE_TIME", null),
            ("Phòng cho nuôi thú cưng", null, null, "PET_FRIENDLY", null),
            ("Phòng có bảo vệ", null, null, "SECURITY", null),
            ("Có bếp gần HUTECH", null, null, "KITCHEN", "hutech"),
            ("Gần SPKT dưới 3 triệu", 3_000_000, null, null, "spkt"),
            ("Đại học sư phạm kỹ thuật 2 người", null, 2, null, "su pham ky thuat"),
            ("Gần ĐHQG phòng 4tr", 4_000_000, null, null, "dhqg"),
            ("Gần nhà văn hóa sinh viên có wifi", null, null, "WIFI", "nha van hoa sinh vien"),
        ];
        foreach (var fixture in fixtures)
        {
            yield return [fixture.Text, fixture.Budget, fixture.People, fixture.Required, fixture.Destination];
            yield return ["Toi muon " + RentalSearchIntent.Normalize(fixture.Text), fixture.Budget, fixture.People, fixture.Required, fixture.Destination];
        }
    }

    [Theory]
    [MemberData(nameof(VietnameseQueries))]
    public void ParsesFiftyVietnameseFixtures(string text, decimal? budget, int? people, string? required, string? destination)
    {
        var intent = RentalSearchIntent.Apply(text);
        Assert.Equal(budget, intent.PriceMax);
        Assert.Equal(people, intent.Occupants);
        Assert.Equal(destination, intent.Destination);
        if (required is not null) Assert.Contains(required, intent.RequiredAmenities);
    }

    [Theory]
    [InlineData("bếp", "KITCHEN")]
    [InlineData("máy lạnh", "AIR_CONDITIONER")]
    [InlineData("điều hòa", "AIR_CONDITIONER")]
    [InlineData("wifi", "WIFI")]
    [InlineData("internet", "WIFI")]
    [InlineData("bãi xe", "PARKING")]
    [InlineData("giữ xe", "PARKING")]
    [InlineData("gửi xe", "PARKING")]
    [InlineData("wc riêng", "PRIVATE_TOILET")]
    [InlineData("vệ sinh riêng", "PRIVATE_TOILET")]
    [InlineData("thú cưng", "PET_FRIENDLY")]
    public void TwentyCorrectionConversationsRemoveOldConditions(string phrase, string code)
    {
        foreach (var remove in new[] { "Không cần ", "Ít quan tâm " })
        {
            var first = RentalSearchIntent.Apply($"2 người tối đa 4tr có {phrase}");
            Assert.Contains(code, first.RequiredAmenities);
            var next = RentalSearchIntent.Apply(remove + phrase, first);
            Assert.DoesNotContain(code, next.RequiredAmenities);
            Assert.DoesNotContain(code, next.ExcludedAmenities);
            Assert.DoesNotContain(code, next.Criteria);
            Assert.Equal(4_000_000, next.PriceMax);
            Assert.Equal(2, next.Occupants);
        }
    }

    [Fact]
    public void ExclusionsSoftConstraintsAndBudgetAreDistinct()
    {
        var intent = RentalSearchIntent.Apply("Không ở ghép, ưu tiên wifi, không có máy lạnh, cả phí tối đa 4tr");
        Assert.True(intent.ExcludeShared);
        Assert.Contains("WIFI", intent.Criteria);
        Assert.DoesNotContain("WIFI", intent.RequiredAmenities);
        Assert.Contains("AIR_CONDITIONER", intent.ExcludedAmenities);
        Assert.Equal("total", intent.BudgetKind);
        Assert.NotEmpty(intent.Unknown);
        var cheaper = RentalSearchIntent.Apply("Rẻ hơn", intent);
        Assert.Equal(3_600_000, cheaper.PriceMax);
    }

    [Fact]
    public void PriceRangeAndAreaAreParsed()
    {
        var intent = RentalSearchIntent.Apply("Từ 2tr đến 4tr, tối thiểu 25m2");
        Assert.Equal(2_000_000, intent.PriceMin);
        Assert.Equal(4_000_000, intent.PriceMax);
        Assert.Equal(25, intent.AreaMin);
        var corrected = RentalSearchIntent.Apply("Bỏ diện tích", intent);
        Assert.Null(corrected.AreaMin);
        Assert.Null(corrected.AreaMax);
        Assert.Equal(intent.PriceMax, corrected.PriceMax);
    }

    [Fact]
    public void HardConstraintsCannotBeOverriddenByDescriptionOrPremium()
    {
        var post = CreatePost();
        var today = new DateOnly(2026, 10, 8);
        Assert.False(AiSearchService.Fits(post, RentalSearchIntent.Apply("Dưới 2tr"), today));
        Assert.False(AiSearchService.Fits(post, RentalSearchIntent.Apply("3 người"), today));
        Assert.False(AiSearchService.Fits(post, RentalSearchIntent.Apply("Có máy lạnh"), today));
        Assert.True(AiSearchService.Fits(post, RentalSearchIntent.Apply("Có bếp, dưới 4tr, 2 người"), today));
        var intent = RentalSearchIntent.Apply("Có bếp, dưới 4tr");
        var regular = AiSearchService.EvaluateFit(post, intent, false);
        var premium = AiSearchService.EvaluateFit(post, intent, true);
        Assert.Equal(regular.UserFit, premium.UserFit);
        Assert.NotEqual(regular.CommercialBoost, premium.CommercialBoost);
        Assert.All(regular.Evidence, evidence => { Assert.Equal(post.Id, evidence.PostId); Assert.Equal("ownerListing", evidence.SourceType); Assert.NotEmpty(evidence.Field); Assert.Equal(post.UpdatedAt, evidence.UpdatedAt); });
        Assert.DoesNotContain(regular.Evidence, evidence => evidence.Field.Contains("review", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void HiddenAndOutsideScopePostsAreRejected()
    {
        var post = CreatePost();
        post.Archive(DateTimeOffset.UtcNow);
        Assert.False(AiSearchService.Fits(post, RentalSearchIntent.Empty(), new(2026, 10, 8)));
        var outside = CreatePost(latitude: 21.02m);
        Assert.False(AiSearchService.Fits(outside, RentalSearchIntent.Empty(), new(2026, 10, 8)));
    }

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
