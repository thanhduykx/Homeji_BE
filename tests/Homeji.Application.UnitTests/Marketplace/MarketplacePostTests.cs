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
}
