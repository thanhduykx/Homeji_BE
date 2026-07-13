namespace Homeji.Application.DTOs.Reviews;

public sealed record RentalReviewDto(
    Guid Id,
    Guid RentalPostId,
    Guid ReviewerId,
    string ReviewerDisplayName,
    string? ReviewerAvatarPath,
    int Rating,
    string? Comment,
    int? LocationRating,
    int? ValueRating,
    int? AmenitiesRating,
    int? SecurityRating,
    int? CleanlinessRating,
    int? AccuracyRating,
    int? LandlordRating,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
