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
        DateTimeOffset createdAt)
    {
        if (rentalPostId == Guid.Empty)
        {
            throw new DomainException("Rental post id must not be empty.");
        }

        if (reviewerId == Guid.Empty)
        {
            throw new DomainException("Reviewer id must not be empty.");
        }

        Id = Guid.NewGuid();
        RentalPostId = rentalPostId;
        ReviewerId = reviewerId;
        SetContent(rating, comment);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RentalPostId { get; private set; }

    public Guid ReviewerId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(int rating, string? comment, DateTimeOffset updatedAt)
    {
        SetContent(rating, comment);
        UpdatedAt = updatedAt;
    }

    private void SetContent(int rating, string? comment)
    {
        if (rating is < 1 or > 5)
        {
            throw new DomainException("Rating must be between 1 and 5.");
        }

        var normalizedComment = comment?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedComment))
        {
            normalizedComment = null;
        }

        if (normalizedComment?.Length > MaxCommentLength)
        {
            throw new DomainException($"Comment must not exceed {MaxCommentLength} characters.");
        }

        Rating = rating;
        Comment = normalizedComment;
    }
}
