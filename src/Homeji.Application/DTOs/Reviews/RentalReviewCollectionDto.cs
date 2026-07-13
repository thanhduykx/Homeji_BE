namespace Homeji.Application.DTOs.Reviews;

public sealed record RentalReviewCollectionDto(
    Guid RentalPostId,
    decimal AverageRating,
    int ReviewCount,
    IReadOnlyList<RentalReviewDto> Reviews);
