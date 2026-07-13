using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class LandlordVerificationRequest
{
    public const int MaxDocumentUrlLength = 1_000;
    public const int MaxNoteLength = 1_000;

    private LandlordVerificationRequest()
    {
        DocumentUrl = null!;
    }

    public LandlordVerificationRequest(Guid applicantId, string documentUrl, string? note, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ApplicantId = applicantId;
        DocumentUrl = NormalizeRequired(documentUrl, MaxDocumentUrlLength, nameof(DocumentUrl));
        ApplicantNote = NormalizeOptional(note, MaxNoteLength, nameof(ApplicantNote));
        Status = LandlordVerificationStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ApplicantId { get; private set; }
    public string DocumentUrl { get; private set; }
    public string? ApplicantNote { get; private set; }
    public LandlordVerificationStatus Status { get; private set; }
    public string? ReviewNote { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Review(bool approved, Guid reviewedBy, string? note, DateTimeOffset updatedAt)
    {
        if (Status != LandlordVerificationStatus.Pending)
        {
            throw new DomainException("Only pending landlord verification can be reviewed.");
        }

        Status = approved ? LandlordVerificationStatus.Verified : LandlordVerificationStatus.Rejected;
        ReviewNote = NormalizeOptional(note, MaxNoteLength, nameof(ReviewNote));
        ReviewedBy = reviewedBy;
        UpdatedAt = updatedAt;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        return NormalizeOptional(value, maxLength, fieldName)
            ?? throw new DomainException($"{fieldName} is required.");
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }
}
