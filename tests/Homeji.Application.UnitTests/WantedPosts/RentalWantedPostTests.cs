using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.WantedPosts;

public sealed class RentalWantedPostTests
{
    [Fact]
    public void Constructor_NormalizesAndDeduplicatesAmenities()
    {
        var post = CreatePost(["parking", " PARKING ", "wifi"]);

        Assert.Equal(["PARKING", "WIFI"], post.AmenityCodes);
    }

    [Fact]
    public void Update_AfterClose_ThrowsDomainException()
    {
        var post = CreatePost(["wifi"]);
        post.Close(DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Throws<DomainException>(() => post.Update(
            "Need a room",
            "Near the university",
            "Hoa Lac",
            3_000_000,
            1,
            ["wifi"],
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            DateTimeOffset.UtcNow.AddMinutes(2)));
        Assert.Equal(WantedPostStatus.Closed, post.Status);
    }

    private static RentalWantedPost CreatePost(IReadOnlyCollection<string> amenities) =>
        new(
            Guid.NewGuid(),
            "Need a room",
            "Near the university",
            "Hoa Lac",
            3_000_000,
            1,
            amenities,
            DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(1)),
            DateTimeOffset.UtcNow);
}
