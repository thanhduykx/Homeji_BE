namespace Homeji.Application.DTOs.Reviews;

public sealed record RentalReviewDto(
    Guid Id,
    Guid RentalPostId,
    Guid ReviewerId,
    string ReviewerDisplayName,
    string? ReviewerAvatarPath,
    int Rating,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
