using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class MarketplacePost
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 3_000;
    public const int MaxConditionLength = 120;
    public const int MaxCategoryLength = 80;
    public const int MaxAddressLength = 500;
    public const int MaxMediaCount = 10;

    private readonly List<MarketplacePostMedia> _media = [];

    private MarketplacePost()
    {
        Title = null!;
        Description = null!;
        Condition = null!;
        Category = null!;
        Address = null!;
    }

    public MarketplacePost(
        Guid sellerId,
        string title,
        string description,
        decimal price,
        string condition,
        string category,
        string address,
        decimal latitude,
        decimal longitude,
        Guid? linkedRentalPostId,
        IReadOnlyCollection<string> mediaUrls,
        DateTimeOffset createdAt)
    {
        if (sellerId == Guid.Empty)
        {
            throw new DomainException("Seller id must not be empty.");
        }

        Id = Guid.NewGuid();
        SellerId = sellerId;
        Status = MarketplacePostStatus.Active;
        SetDetails(title, description, price, condition, category, address, latitude, longitude, linkedRentalPostId);
        ReplaceMedia(mediaUrls);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid SellerId { get; private set; }

    public MarketplacePostStatus Status { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public decimal Price { get; private set; }

    public string Condition { get; private set; } = null!;

    public string Category { get; private set; } = null!;

    public string Address { get; private set; } = null!;

    public decimal Latitude { get; private set; }

    public decimal Longitude { get; private set; }

    public Guid? LinkedRentalPostId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<MarketplacePostMedia> Media => _media;

    public void Update(
        string title,
        string description,
        decimal price,
        string condition,
        string category,
        string address,
        decimal latitude,
        decimal longitude,
        Guid? linkedRentalPostId,
        IReadOnlyCollection<string> mediaUrls,
        DateTimeOffset updatedAt)
    {
        EnsureActive();
        SetDetails(title, description, price, condition, category, address, latitude, longitude, linkedRentalPostId);
        ReplaceMedia(mediaUrls);
        UpdatedAt = updatedAt;
    }

    public void MarkSold(DateTimeOffset updatedAt)
    {
        EnsureActive();
        Status = MarketplacePostStatus.Sold;
        UpdatedAt = updatedAt;
    }

    public void Archive(DateTimeOffset updatedAt)
    {
        if (Status == MarketplacePostStatus.Archived)
        {
            return;
        }

        Status = MarketplacePostStatus.Archived;
        UpdatedAt = updatedAt;
    }

    private void SetDetails(
        string title,
        string description,
        decimal price,
        string condition,
        string category,
        string address,
        decimal latitude,
        decimal longitude,
        Guid? linkedRentalPostId)
    {
        Title = NormalizeRequired(title, MaxTitleLength, nameof(Title));
        Description = NormalizeRequired(description, MaxDescriptionLength, nameof(Description));
        Price = price > 0 ? price : throw new DomainException("Marketplace price must be greater than zero.");
        Condition = NormalizeRequired(condition, MaxConditionLength, nameof(Condition));
        Category = NormalizeRequired(category, MaxCategoryLength, nameof(Category));
        Address = NormalizeRequired(address, MaxAddressLength, nameof(Address));
        Latitude = ValidateCoordinate(latitude, -90, 90, nameof(Latitude));
        Longitude = ValidateCoordinate(longitude, -180, 180, nameof(Longitude));
        LinkedRentalPostId = linkedRentalPostId == Guid.Empty ? null : linkedRentalPostId;
    }

    private void ReplaceMedia(IReadOnlyCollection<string> mediaUrls)
    {
        var normalizedUrls = mediaUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalizedUrls.Length is < 1 or > MaxMediaCount)
        {
            throw new DomainException($"Marketplace post requires between 1 and {MaxMediaCount} images.");
        }

        _media.Clear();
        for (var index = 0; index < normalizedUrls.Length; index++)
        {
            _media.Add(new MarketplacePostMedia(Id, normalizedUrls[index], index));
        }
    }

    private void EnsureActive()
    {
        if (Status != MarketplacePostStatus.Active)
        {
            throw new DomainException("Only active marketplace posts can be changed.");
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
    }

    private static decimal ValidateCoordinate(decimal value, decimal min, decimal max, string fieldName)
    {
        return value >= min && value <= max
            ? value
            : throw new DomainException($"{fieldName} is out of range.");
    }
}
