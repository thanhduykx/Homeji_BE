using Homeji.Domain.Entities;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalSourceListingTests
{
    [Theory]
    [InlineData("https://file.phongtro.vn/2/photo.jpg", true)]
    [InlineData("http://file.phongtro.vn/2/photo.jpg", false)]
    [InlineData("https://file.phongtro.vn.evil.invalid/photo.jpg", false)]
    [InlineData("https://user@file.phongtro.vn/photo.jpg", false)]
    public void PhongtroVn_UsesOnlyItsOwnHttpsGallery(string image, bool valid)
    {
        RentalSourceListing Create() => RentalSourceListing.Create("330425",
            "https://phongtro.vn/cho-thue-phong-tro-phuong-long-phuoc-tp-ho-chi-minh/330425",
            "Phòng trọ", "Phường Long Phước", "quan-9", 3_500_000, 40, [image], null,
            DateTimeOffset.UtcNow, "phongtrovn");
        if (!valid) { Assert.Throws<DomainException>(Create); return; }
        var listing = Create();
        Assert.Equal("phongtrovn", listing.Source);
        Assert.Null(listing.Latitude);
        Assert.Null(listing.Longitude);
    }

    [Fact]
    public void SourceExpiry_DoesNotChangeSnapshotOrClaimRoomAvailability()
    {
        var collected = new DateTimeOffset(2026, 10, 3, 0, 0, 0, TimeSpan.Zero);
        var listing = RentalSourceListing.Create("123", "https://phongtro123.com/tin-pr123.html",
            "Phòng trọ", "Thủ Đức", "quan-9", 2_000_000, 20, ["https://pt123.cdn.static123.com/photo.jpg"], null, collected);
        Assert.Null(listing.SourceExpiresAt);
        Assert.Null(listing.SourceCheckedAt);
        var expiry = new DateTimeOffset(2026, 9, 12, 10, 55, 0, TimeSpan.FromHours(7));
        var checkedAt = collected.AddDays(2);
        listing.RecordSourceExpiry(expiry, checkedAt);
        Assert.Equal(expiry.ToUniversalTime(), listing.SourceExpiresAt);
        Assert.Equal(checkedAt, listing.SourceCheckedAt);
        Assert.Equal(collected, listing.CollectedAt);
        Assert.Equal(2_000_000, listing.Price);
        Assert.Throws<DomainException>(() => listing.RecordSourceExpiry(null, collected));
        Assert.Equal(expiry.ToUniversalTime(), listing.SourceExpiresAt);
        listing.RecordSourceExpiry(null, checkedAt.AddDays(1));
        Assert.Null(listing.SourceExpiresAt);
    }

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
