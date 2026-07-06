using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RentalPostMedia
{
    public const int MaxBucketLength = 100;
    public const int MaxPathLength = 500;

    private RentalPostMedia()
    {
        Bucket = null!;
        Path = null!;
    }

    private RentalPostMedia(
        Guid id,
        Guid rentalPostId,
        MediaType mediaType,
        string bucket,
        string path,
        bool isThumbnail,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        Id = id;
        RentalPostId = rentalPostId;
        MediaType = mediaType;
        Bucket = Normalize(bucket, MaxBucketLength, nameof(Bucket));
        Path = Normalize(path, MaxPathLength, nameof(Path));
        IsThumbnail = isThumbnail;
        SortOrder = sortOrder;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RentalPostId { get; private set; }

    public MediaType MediaType { get; private set; }

    public string Bucket { get; private set; }

    public string Path { get; private set; }

    public bool IsThumbnail { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static RentalPostMedia Create(
        Guid rentalPostId,
        MediaType mediaType,
        string bucket,
        string path,
        bool isThumbnail,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        if (rentalPostId == Guid.Empty)
        {
            throw new DomainException("Rental post id must not be empty.");
        }

        return new RentalPostMedia(
            Guid.NewGuid(),
            rentalPostId,
            mediaType,
            bucket,
            path,
            isThumbnail,
            sortOrder,
            createdAt);
    }

    private static string Normalize(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }
}
