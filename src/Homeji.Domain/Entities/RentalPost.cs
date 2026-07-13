using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RentalPost
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 4_000;
    public const int MaxAddressLength = 500;
    public const int MaxModerationReasonLength = 500;
    public const int MaxAmenityCodeLength = 60;
    public const int MaxHouseRulesLength = 2_000;
    public const int MinimumImageCountForSubmit = 3;

    private readonly List<RentalPostMedia> _media = [];
    private readonly List<RentalPostAmenity> _amenities = [];

    private RentalPost()
    {
        Title = null!;
        Description = null!;
        Address = null!;
    }

    private RentalPost(Guid id, Guid ownerId, RentalPostType type, DateTimeOffset createdAt)
    {
        Id = id;
        OwnerId = ownerId;
        Type = type;
        Status = RentalPostStatus.Draft;
        Title = string.Empty;
        Description = string.Empty;
        Address = string.Empty;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid OwnerId { get; private set; }

    public RentalPostType Type { get; private set; }

    public RentalPostStatus Status { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public decimal Price { get; private set; }

    public decimal Deposit { get; private set; }

    public decimal Area { get; private set; }
    public decimal ElectricityPrice { get; private set; }
    public decimal WaterPrice { get; private set; }
    public decimal InternetPrice { get; private set; }
    public int MaxOccupants { get; private set; } = 1;
    public int AvailableSlots { get; private set; } = 1;
    public string? HouseRules { get; private set; }
    public DateOnly? AvailableFrom { get; private set; }

    public string Address { get; private set; }

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public string? ModerationReason { get; private set; }

    public int ViewCount { get; private set; }

    public int SaveCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<RentalPostMedia> Media => _media;

    public IReadOnlyCollection<RentalPostAmenity> Amenities => _amenities;

    public static RentalPost CreateDraft(Guid ownerId, RentalPostType type, DateTimeOffset createdAt)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("Owner id must not be empty.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Rental post type is invalid.");
        }

        return new RentalPost(Guid.NewGuid(), ownerId, type, createdAt);
    }

    public void UpdateDetails(
        RentalPostType type,
        string title,
        string description,
        decimal price,
        decimal deposit,
        decimal area,
        string address,
        decimal latitude,
        decimal longitude,
        IEnumerable<string> amenityCodes,
        DateTimeOffset updatedAt,
        decimal electricityPrice = 0,
        decimal waterPrice = 0,
        decimal internetPrice = 0,
        int maxOccupants = 1,
        int availableSlots = 1,
        string? houseRules = null,
        DateOnly? availableFrom = null)
    {
        EnsureEditable();
        var wasActive = Status == RentalPostStatus.Active;
        Type = type;
        Title = NormalizeRequired(title, MaxTitleLength, nameof(Title));
        Description = NormalizeRequired(description, MaxDescriptionLength, nameof(Description));
        Price = RequirePositive(price, nameof(Price));
        Deposit = deposit < 0 ? throw new DomainException("Deposit must not be negative.") : deposit;
        Area = RequirePositive(area, nameof(Area));
        ElectricityPrice = RequireNonNegative(electricityPrice, nameof(ElectricityPrice));
        WaterPrice = RequireNonNegative(waterPrice, nameof(WaterPrice));
        InternetPrice = RequireNonNegative(internetPrice, nameof(InternetPrice));
        if (maxOccupants <= 0 || availableSlots <= 0 || availableSlots > maxOccupants)
        {
            throw new DomainException("Available slots must be between 1 and the maximum occupants.");
        }

        MaxOccupants = maxOccupants;
        AvailableSlots = availableSlots;
        HouseRules = NormalizeOptional(houseRules, MaxHouseRulesLength, nameof(HouseRules));
        AvailableFrom = availableFrom;
        Address = NormalizeRequired(address, MaxAddressLength, nameof(Address));
        Latitude = ValidateCoordinate(latitude, -90, 90, nameof(Latitude));
        Longitude = ValidateCoordinate(longitude, -180, 180, nameof(Longitude));
        _amenities.Clear();

        foreach (var code in amenityCodes.Select(NormalizeAmenity).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            _amenities.Add(new RentalPostAmenity(Id, code));
        }

        UpdatedAt = updatedAt;
        if (wasActive)
        {
            Status = RentalPostStatus.Pending;
            ModerationReason = null;
        }
    }

    public RentalPostMedia AddMedia(
        MediaType mediaType,
        string bucket,
        string path,
        bool isThumbnail,
        int sortOrder,
        DateTimeOffset createdAt)
    {
        EnsureEditable();
        var wasActive = Status == RentalPostStatus.Active;
        var media = RentalPostMedia.Create(Id, mediaType, bucket, path, isThumbnail, sortOrder, createdAt);
        _media.Add(media);
        UpdatedAt = createdAt;
        if (wasActive)
        {
            Status = RentalPostStatus.Pending;
        }

        return media;
    }

    public void RemoveMedia(Guid mediaId, DateTimeOffset updatedAt)
    {
        EnsureEditable();
        var wasActive = Status == RentalPostStatus.Active;
        var media = _media.SingleOrDefault(item => item.Id == mediaId);
        if (media is null)
        {
            throw new DomainException("Media was not found on this rental post.");
        }

        _media.Remove(media);
        UpdatedAt = updatedAt;
        if (wasActive)
        {
            Status = RentalPostStatus.Pending;
        }
    }

    public void Submit(DateTimeOffset submittedAt)
    {
        EnsureEditable();
        if (string.IsNullOrWhiteSpace(Title) || string.IsNullOrWhiteSpace(Description) || string.IsNullOrWhiteSpace(Address))
        {
            throw new DomainException("Rental post details must be completed before submitting.");
        }

        if (_media.Count(item => item.MediaType == MediaType.Image) < MinimumImageCountForSubmit)
        {
            throw new DomainException($"Rental post requires at least {MinimumImageCountForSubmit} images.");
        }

        Status = RentalPostStatus.Pending;
        ModerationReason = null;
        UpdatedAt = submittedAt;
    }

    public void Approve(DateTimeOffset approvedAt)
    {
        if (Status != RentalPostStatus.Pending)
        {
            throw new DomainException("Only pending rental posts can be approved.");
        }

        Status = RentalPostStatus.Active;
        ModerationReason = null;
        UpdatedAt = approvedAt;
    }

    public void Reject(string reason, DateTimeOffset rejectedAt)
    {
        if (Status != RentalPostStatus.Pending)
        {
            throw new DomainException("Only pending rental posts can be rejected.");
        }

        Status = RentalPostStatus.Rejected;
        ModerationReason = NormalizeRequired(reason, MaxModerationReasonLength, nameof(reason));
        UpdatedAt = rejectedAt;
    }

    public void Archive(DateTimeOffset archivedAt)
    {
        if (Status is RentalPostStatus.Archived or RentalPostStatus.Rented)
        {
            return;
        }

        Status = RentalPostStatus.Archived;
        UpdatedAt = archivedAt;
    }

    public void MarkRented(DateTimeOffset rentedAt)
    {
        if (Status != RentalPostStatus.Active)
        {
            throw new DomainException("Only active rental posts can be marked as rented.");
        }

        Status = RentalPostStatus.Rented;
        UpdatedAt = rentedAt;
    }

    public void IncrementViewCount() => ViewCount++;

    public void ApplySaveDelta(int delta)
    {
        SaveCount = Math.Max(0, SaveCount + delta);
    }

    private void EnsureEditable()
    {
        if (Status is RentalPostStatus.Archived or RentalPostStatus.Rented)
        {
            throw new DomainException("Rental post is not editable in its current status.");
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
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

    private static string NormalizeAmenity(string code)
    {
        return NormalizeRequired(code, MaxAmenityCodeLength, nameof(code)).ToUpperInvariant();
    }

    private static decimal RequirePositive(decimal value, string fieldName)
    {
        return value <= 0 ? throw new DomainException($"{fieldName} must be greater than zero.") : value;
    }

    private static decimal RequireNonNegative(decimal value, string fieldName)
    {
        return value < 0 ? throw new DomainException($"{fieldName} must not be negative.") : value;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
    }

    private static decimal ValidateCoordinate(decimal value, decimal min, decimal max, string fieldName)
    {
        return value < min || value > max
            ? throw new DomainException($"{fieldName} is out of range.")
            : value;
    }
}
