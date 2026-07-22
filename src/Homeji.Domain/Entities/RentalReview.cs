using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RentalReview
{
    public const int MaxCommentLength = 1_000;

    private RentalReview()
    {
    }

    public RentalReview(
        Guid rentalPostId,
        Guid reviewerId,
        int rating,
        string? comment,
        DateTimeOffset createdAt,
        int? locationRating = null,
        int? valueRating = null,
        int? amenitiesRating = null,
        int? securityRating = null,
        int? cleanlinessRating = null,
        int? accuracyRating = null,
        int? landlordRating = null)
    {
        if (rentalPostId == Guid.Empty)
        {
            throw new DomainException("Mã tin đăng không được để trống.");
        }

        if (reviewerId == Guid.Empty)
        {
            throw new DomainException("Mã người đánh giá không được để trống.");
        }

        Id = Guid.NewGuid();
        RentalPostId = rentalPostId;
        ReviewerId = reviewerId;
        SetContent(
            rating,
            comment,
            locationRating,
            valueRating,
            amenitiesRating,
            securityRating,
            cleanlinessRating,
            accuracyRating,
            landlordRating);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RentalPostId { get; private set; }

    public Guid ReviewerId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }
    public int? LocationRating { get; private set; }
    public int? ValueRating { get; private set; }
    public int? AmenitiesRating { get; private set; }
    public int? SecurityRating { get; private set; }
    public int? CleanlinessRating { get; private set; }
    public int? AccuracyRating { get; private set; }
    public int? LandlordRating { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        int rating,
        string? comment,
        DateTimeOffset updatedAt,
        int? locationRating = null,
        int? valueRating = null,
        int? amenitiesRating = null,
        int? securityRating = null,
        int? cleanlinessRating = null,
        int? accuracyRating = null,
        int? landlordRating = null)
    {
        SetContent(
            rating,
            comment,
            locationRating,
            valueRating,
            amenitiesRating,
            securityRating,
            cleanlinessRating,
            accuracyRating,
            landlordRating);
        UpdatedAt = updatedAt;
    }

    private void SetContent(
        int rating,
        string? comment,
        int? locationRating,
        int? valueRating,
        int? amenitiesRating,
        int? securityRating,
        int? cleanlinessRating,
        int? accuracyRating,
        int? landlordRating)
    {
        if (rating is < 1 or > 5)
        {
            throw new DomainException("Điểm đánh giá phải từ 1 đến 5.");
        }

        var normalizedComment = comment?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedComment))
        {
            normalizedComment = null;
        }

        if (normalizedComment?.Length > MaxCommentLength)
        {
            throw new DomainException($"Bình luận không được vượt quá {MaxCommentLength} ký tự.");
        }

        Rating = rating;
        Comment = normalizedComment;
        LocationRating = ValidateOptionalRating(locationRating, nameof(LocationRating));
        ValueRating = ValidateOptionalRating(valueRating, nameof(ValueRating));
        AmenitiesRating = ValidateOptionalRating(amenitiesRating, nameof(AmenitiesRating));
        SecurityRating = ValidateOptionalRating(securityRating, nameof(SecurityRating));
        CleanlinessRating = ValidateOptionalRating(cleanlinessRating, nameof(CleanlinessRating));
        AccuracyRating = ValidateOptionalRating(accuracyRating, nameof(AccuracyRating));
        LandlordRating = ValidateOptionalRating(landlordRating, nameof(LandlordRating));
    }

    private static int? ValidateOptionalRating(int? value, string fieldName)
    {
        return value is null or >= 1 and <= 5
            ? value
            : throw new DomainException($"{fieldName} phải từ 1 đến 5.");
    }
}
