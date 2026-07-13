using Homeji.Domain.Entities;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Reviews;

public sealed class RentalReviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Constructor_WithRatingOutsideRange_ThrowsDomainException(int rating)
    {
        var exception = Assert.Throws<DomainException>(() => new RentalReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            rating,
            null,
            DateTimeOffset.UtcNow));

        Assert.Equal("Rating must be between 1 and 5.", exception.Message);
    }

    [Fact]
    public void Update_NormalizesCommentAndChangesContent()
    {
        var createdAt = new DateTimeOffset(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);
        var updatedAt = createdAt.AddHours(1);
        var review = new RentalReview(Guid.NewGuid(), Guid.NewGuid(), 3, "  good room  ", createdAt);

        review.Update(5, "  excellent  ", updatedAt);

        Assert.Equal(5, review.Rating);
        Assert.Equal("excellent", review.Comment);
        Assert.Equal(updatedAt, review.UpdatedAt);
    }

    [Fact]
    public void Constructor_WithCriteriaRatings_PreservesScores()
    {
        var review = new RentalReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            "Good",
            DateTimeOffset.UtcNow,
            locationRating: 5,
            valueRating: 4,
            amenitiesRating: 3,
            securityRating: 5,
            cleanlinessRating: 4,
            accuracyRating: 4,
            landlordRating: 5);

        Assert.Equal(5, review.LocationRating);
        Assert.Equal(3, review.AmenitiesRating);
        Assert.Equal(5, review.LandlordRating);
    }

    [Fact]
    public void Constructor_WithCriteriaRatingOutsideRange_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new RentalReview(
            Guid.NewGuid(),
            Guid.NewGuid(),
            4,
            null,
            DateTimeOffset.UtcNow,
            locationRating: 6));
    }
}
