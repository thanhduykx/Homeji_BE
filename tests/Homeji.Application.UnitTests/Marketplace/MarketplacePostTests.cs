using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class MarketplacePostTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesActivePostAndMedia()
    {
        var post = CreatePost();

        Assert.Equal(MarketplacePostStatus.Active, post.Status);
        Assert.Single(post.Media);
        Assert.Equal("https://cdn.example.com/item.jpg", post.Media.Single().Url);
    }

    [Fact]
    public void Constructor_WithoutMedia_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new MarketplacePost(
            Guid.NewGuid(),
            "Bàn học",
            "Bàn còn tốt",
            300_000,
            "Đã sử dụng",
            "Nội thất",
            "Thủ Đức",
            10.85m,
            106.77m,
            null,
            [],
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Update_AfterSold_ThrowsDomainException()
    {
        var post = CreatePost();
        post.MarkSold(DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<DomainException>(() => post.Update(
            "Bàn học mới",
            "Mô tả mới",
            350_000,
            "Đã sử dụng",
            "Nội thất",
            "Thủ Đức",
            10.85m,
            106.77m,
            null,
            ["https://cdn.example.com/item-2.jpg"],
            DateTimeOffset.UtcNow.AddMinutes(2)));
    }

    [Fact]
    public void FoodReservation_ReleaseAndCompletion_PreserveStockLedger()
    {
        var now = DateTimeOffset.UtcNow;
        var post = CreateFoodPost(now, 10);

        post.Reserve(3, now.AddMinutes(1));
        Assert.Equal(7, post.AvailableQuantity);
        Assert.Equal(3, post.ReservedQuantity);

        post.ReleaseReservation(1, now.AddMinutes(2));
        post.CompleteReservation(2, now.AddMinutes(3));

        Assert.Equal(8, post.AvailableQuantity);
        Assert.Equal(0, post.ReservedQuantity);
        Assert.Equal(MarketplacePostStatus.Active, post.Status);
    }

    [Fact]
    public void Update_WhenStockIsReserved_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var post = CreateFoodPost(now, 10);
        post.Reserve(1, now.AddMinutes(1));

        Assert.Throws<DomainException>(() => post.Update(
            "Cơm gà mới",
            "Mô tả mới",
            39_000,
            "Mới làm trong ngày",
            "Cơm nhà",
            "Thủ Đức",
            10.85m,
            106.77m,
            null,
            ["https://cdn.example.com/rice.jpg"],
            now.AddMinutes(2),
            MarketplaceListingType.Food,
            12,
            "phần",
            15));
    }

    private static MarketplacePost CreatePost()
    {
        return new MarketplacePost(
            Guid.NewGuid(),
            "Bàn học",
            "Bàn còn tốt",
            300_000,
            "Đã sử dụng",
            "Nội thất",
            "Thủ Đức",
            10.85m,
            106.77m,
            null,
            ["https://cdn.example.com/item.jpg"],
            DateTimeOffset.UtcNow);
    }

    private static MarketplacePost CreateFoodPost(DateTimeOffset now, int quantity) =>
        new(
            Guid.NewGuid(),
            "Cơm gà",
            "Cơm gà cho sinh viên",
            35_000,
            "Mới làm trong ngày",
            "Cơm nhà",
            "Thủ Đức",
            10.85m,
            106.77m,
            null,
            ["https://cdn.example.com/rice.jpg"],
            now,
            MarketplaceListingType.Food,
            quantity,
            "phần",
            15);
}
