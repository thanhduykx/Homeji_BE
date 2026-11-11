using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.Services.Marketplace.Validation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class FoodPostDomainTests
{
    private static readonly Guid TestSellerId = Guid.NewGuid();
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateFoodPost_WithoutCondition_Succeeds()
    {
        var post = new MarketplacePost(
            TestSellerId,
            "Cơm tấm sườn bì chả",
            "Cơm tấm nóng hổi sườn nướng than hoa",
            35_000,
            condition: null,
            category: "Cơm",
            address: "Khu B ĐHQG, Dĩ An",
            latitude: 10.8805m,
            longitude: 106.7820m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/comtam.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 20,
            unit: "dĩa",
            preparationMinutes: 15,
            estimatedCost: 18_000,
            dailyQuota: 30,
            freshnessBadge: "CookToOrder",
            allergenAlerts: "Không cay",
            isSamplePhoto: false);

        Assert.Equal(MarketplacePostStatus.Active, post.Status);
        Assert.Equal(string.Empty, post.Condition);
        Assert.Equal(MarketplaceListingType.Food, post.ListingType);
        Assert.Equal(35_000, post.Price);
        Assert.Equal(18_000, post.EstimatedCost);
        Assert.Equal(30, post.DailyQuota);
        Assert.Equal(20, post.AvailableQuantity);
        Assert.Equal("CookToOrder", post.FreshnessBadge);
        Assert.Equal("Không cay", post.AllergenAlerts);
        Assert.False(post.IsSamplePhoto);
    }

    [Fact]
    public void CreateSecondHandPost_WithoutCondition_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new MarketplacePost(
            TestSellerId,
            "Bàn học sinh viên",
            "Bàn gỗ tự nhiên",
            150_000,
            condition: null!,
            category: "Nội thất",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/table.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.SecondHand));
    }

    [Fact]
    public void FoodPost_FinancialFormulas_CalculateCorrectly()
    {
        var post = new MarketplacePost(
            TestSellerId,
            "Bún bò Huế đặc biệt",
            "Bún bò chuẩn vị Huế",
            40_000,
            condition: "",
            category: "Bún",
            address: "Linh Trung, Thủ Đức",
            latitude: 10.86m,
            longitude: 106.77m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/bunbo.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 15,
            unit: "tô",
            preparationMinutes: 10,
            estimatedCost: 22_000);

        // Price = 40,000đ
        // Platform fee 10% = 4,000đ -> Seller net revenue = 36,000đ
        var netRevenue = post.CalculateSellerNetRevenue(0.10m);
        Assert.Equal(36_000m, netRevenue);

        // Estimated profit = 36,000 - 22,000 = 14,000đ
        var profit = post.CalculateEstimatedProfit(0.10m);
        Assert.Equal(14_000m, profit);

        // Margin % = 14,000 / 40,000 * 100 = 35%
        var margin = post.CalculateMarginPercentage(0.10m);
        Assert.Equal(35.00m, margin);
    }

    [Fact]
    public void FoodPost_StockReservation_And_Restoration_MaintainsIntegrity()
    {
        var post = new MarketplacePost(
            TestSellerId,
            "Trà đào cam sả",
            "Nước uống giải khát mát lạnh",
            20_000,
            condition: "",
            category: "Đồ uống",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/tradao.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 25,
            unit: "ly",
            preparationMinutes: 5,
            estimatedCost: 8_000,
            dailyQuota: 25);

        // Khách đặt 5 ly
        post.Reserve(5, Now.AddMinutes(1));
        Assert.Equal(20, post.AvailableQuantity);
        Assert.Equal(5, post.ReservedQuantity);

        // Hủy 2 ly -> Hoàn lại kho
        post.ReleaseReservation(2, Now.AddMinutes(2));
        Assert.Equal(22, post.AvailableQuantity);
        Assert.Equal(3, post.ReservedQuantity);

        // Hoàn tất 3 ly còn lại -> Món ăn vẫn ở trạng thái Active (không bị chuyển sang Sold như đồ cũ)
        post.CompleteReservation(3, Now.AddMinutes(3));
        Assert.Equal(22, post.AvailableQuantity);
        Assert.Equal(0, post.ReservedQuantity);
        Assert.Equal(MarketplacePostStatus.Active, post.Status);
    }

    [Fact]
    public void FoodPost_OverReservation_ThrowsDomainException()
    {
        var post = new MarketplacePost(
            TestSellerId,
            "Cơm chiên dương châu",
            "Cơm chiên hạt tơi",
            30_000,
            condition: "",
            category: "Cơm",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/comchien.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 5);

        Assert.Throws<DomainException>(() => post.Reserve(6, Now.AddMinutes(1)));
    }

    [Fact]
    public void FoodPost_NegativeEstimatedCost_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new MarketplacePost(
            TestSellerId,
            "Món lỗi giá vốn",
            "Mô tả",
            30_000,
            condition: "",
            category: "Cơm",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/err.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 10,
            estimatedCost: -5_000));
    }

    [Fact]
    public void KitchenProfile_ManageStatusAndDetails()
    {
        var profile = new KitchenProfile(TestSellerId, "Bếp Sinh Viên Nhà B4", "Chuyên cơm sinh viên giá rẻ");
        Assert.Equal("Bếp Sinh Viên Nhà B4", profile.KitchenName);
        Assert.True(profile.IsOpenNow);
        Assert.Equal("Chuyên cơm sinh viên giá rẻ", profile.KitchenBio);

        // Tạm đóng nhận đơn
        profile.ToggleOpenStatus(false, Now.AddHours(1));
        Assert.False(profile.IsOpenNow);

        // Cập nhật thông tin bếp
        profile.Update("Bếp B4 Món Ngon", true, 3, 3.5m, "Mô tả mới", "https://cert.example.com/attt.pdf", Now.AddHours(2));
        Assert.Equal("Bếp B4 Món Ngon", profile.KitchenName);
        Assert.True(profile.IsOpenNow);
        Assert.Equal(3.5m, profile.MaxDeliveryDistanceKm);
        Assert.Equal("https://cert.example.com/attt.pdf", profile.HygieneCertificateUrl);
    }

    [Fact]
    public void UpsertMarketplacePostDtoValidator_FoodListing_ValidatesCorrectly()
    {
        var validator = new UpsertMarketplacePostDtoValidator();

        // Món hợp lệ: Condition rỗng, giá >= 10.000, prepMinutes 15 (5-120)
        var validFood = new UpsertMarketplacePostDto(
            Title: "Bánh mì chảo",
            Description: "Bánh mì chảo xúc xích trứng ốp la",
            Price: 25_000,
            Condition: null,
            Category: "Ăn sáng",
            Address: "Khu B KTX ĐHQG",
            Latitude: 10.88m,
            Longitude: 106.78m,
            LinkedRentalPostId: null,
            MediaUrls: ["https://example.com/banhmi.jpg"],
            ListingType: MarketplaceListingType.Food,
            AvailableQuantity: 10,
            PreparationMinutes: 15);

        var validResult = validator.Validate(validFood);
        Assert.True(validResult.IsValid);

        // Giá < 10.000 -> Báo lỗi giá sàn
        var cheapFood = validFood with { Price = 8_000 };
        var cheapResult = validator.Validate(cheapFood);
        Assert.False(cheapResult.IsValid);
        Assert.Contains(cheapResult.Errors, e => e.PropertyName == "Price");

        // Thời gian chuẩn bị < 5 phút -> Báo lỗi
        var quickFood = validFood with { PreparationMinutes = 2 };
        var quickResult = validator.Validate(quickFood);
        Assert.False(quickResult.IsValid);
        Assert.Contains(quickResult.Errors, e => e.PropertyName == "PreparationMinutes");

        // Thời gian chuẩn bị > 120 phút -> Báo lỗi
        var slowFood = validFood with { PreparationMinutes = 150 };
        var slowResult = validator.Validate(slowFood);
        Assert.False(slowResult.IsValid);
        Assert.Contains(slowResult.Errors, e => e.PropertyName == "PreparationMinutes");

        // Đồ cũ không có condition -> Báo lỗi condition
        var secondhandWithoutCondition = validFood with
        {
            ListingType = MarketplaceListingType.SecondHand,
            Condition = null,
            PreparationMinutes = null
        };
        var shResult = validator.Validate(secondhandWithoutCondition);
        Assert.False(shResult.IsValid);
        Assert.Contains(shResult.Errors, e => e.PropertyName == "Condition");

        // EstimatedCost âm -> Báo lỗi
        var negativeCostFood = validFood with { EstimatedCost = -5_000 };
        var negCostResult = validator.Validate(negativeCostFood);
        Assert.False(negCostResult.IsValid);
        Assert.Contains(negCostResult.Errors, e => e.PropertyName == "EstimatedCost");

        // DailyQuota <= 0 hoặc > 100 -> Báo lỗi
        var zeroQuotaFood = validFood with { DailyQuota = 0 };
        var zeroQuotaResult = validator.Validate(zeroQuotaFood);
        Assert.False(zeroQuotaResult.IsValid);
        Assert.Contains(zeroQuotaResult.Errors, e => e.PropertyName == "DailyQuota");

        var overQuotaFood = validFood with { DailyQuota = 150 };
        var overQuotaResult = validator.Validate(overQuotaFood);
        Assert.False(overQuotaResult.IsValid);
        Assert.Contains(overQuotaResult.Errors, e => e.PropertyName == "DailyQuota");
    }

    [Fact]
    public void FoodPost_BelowMinimumPrice_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new MarketplacePost(
            TestSellerId,
            "Trà đá giá rẻ",
            "Trà đá giải khát",
            5_000,
            condition: "",
            category: "Đồ uống",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/trada.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 10));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(150)]
    public void FoodPost_InvalidPreparationMinutes_ThrowsDomainException(int prepMinutes)
    {
        Assert.Throws<DomainException>(() => new MarketplacePost(
            TestSellerId,
            "Cơm sườn",
            "Mô tả",
            30_000,
            condition: "",
            category: "Cơm",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/com.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 10,
            preparationMinutes: prepMinutes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void FoodPost_InvalidDailyQuota_ThrowsDomainException(int quota)
    {
        Assert.Throws<DomainException>(() => new MarketplacePost(
            TestSellerId,
            "Cơm sườn",
            "Mô tả",
            30_000,
            condition: "",
            category: "Cơm",
            address: "Thủ Đức",
            latitude: 10.85m,
            longitude: 106.78m,
            linkedRentalPostId: null,
            mediaUrls: ["https://example.com/com.jpg"],
            createdAt: Now,
            listingType: MarketplaceListingType.Food,
            availableQuantity: 10,
            dailyQuota: quota));
    }
}
