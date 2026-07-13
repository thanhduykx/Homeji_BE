namespace Homeji.Application.DTOs.Reviews;

public sealed record RentalReviewCollectionDto(
    Guid RentalPostId,
    decimal AverageRating,
    int ReviewCount,
    RentalReviewRatingSummaryDto CriteriaAverages,
    IReadOnlyList<RentalReviewDto> Reviews);

public sealed record RentalReviewRatingSummaryDto(
    decimal? Location,
    decimal? Value,
    decimal? Amenities,
    decimal? Security,
    decimal? Cleanliness,
    decimal? Accuracy,
    decimal? Landlord);
