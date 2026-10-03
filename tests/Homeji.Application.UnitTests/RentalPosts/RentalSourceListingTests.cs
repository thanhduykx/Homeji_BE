using Homeji.Domain.Entities;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalSourceListingTests
{
    [Theory]
    [InlineData("ha-noi", "https://pt123.cdn.static123.com/photo.jpg")]
    [InlineData("quan-9", "http://pt123.cdn.static123.com/photo.jpg")]
    [InlineData("quan-9", "https://pt123.cdn.static123.com.evil.invalid/photo.jpg")]
    [InlineData("quan-9", "https://user@pt123.cdn.static123.com/photo.jpg")]
    public void Untrusted_geography_or_image_origin_is_rejected(string district, string image)
    {
        Assert.Throws<DomainException>(() => RentalSourceListing.Create("123",
            "https://phongtro123.com/tin-pr123.html", "Phòng trọ", "Thủ Đức", district,
            2_000_000, 20, [image], null, DateTimeOffset.UtcNow));
    }
}
