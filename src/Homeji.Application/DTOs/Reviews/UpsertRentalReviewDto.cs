namespace Homeji.Application.DTOs.Reviews;

public sealed record UpsertRentalReviewDto(
    int Rating,
    string? Comment,
    int? LocationRating = null,
    int? ValueRating = null,
    int? AmenitiesRating = null,
    int? SecurityRating = null,
    int? CleanlinessRating = null,
    int? AccuracyRating = null,
    int? LandlordRating = null);
