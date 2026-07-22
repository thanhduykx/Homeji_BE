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
    public const int MaxUnitLength = 30;
    public const int MaxFoodStock = 100;

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
        DateTimeOffset createdAt,
        MarketplaceListingType listingType = MarketplaceListingType.SecondHand,
        int availableQuantity = 1,
        string unit = "món",
        int? preparationMinutes = null)
    {
        if (sellerId == Guid.Empty)
        {
            throw new DomainException("Mã người bán không được để trống.");
        }

        Id = Guid.NewGuid();
        SellerId = sellerId;
        Status = MarketplacePostStatus.Active;
        SetDetails(title, description, price, condition, category, address, latitude, longitude, linkedRentalPostId,
            listingType, availableQuantity, unit, preparationMinutes);
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

    public MarketplaceListingType ListingType { get; private set; }

    public int AvailableQuantity { get; private set; }

    public int ReservedQuantity { get; private set; }

    public string Unit { get; private set; } = null!;

    public int? PreparationMinutes { get; private set; }

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
        DateTimeOffset updatedAt,
        MarketplaceListingType listingType = MarketplaceListingType.SecondHand,
        int availableQuantity = 1,
        string unit = "món",
        int? preparationMinutes = null)
    {
        EnsureActive();
        if (ReservedQuantity > 0)
        {
            throw new DomainException("Marketplace posts with active reservations cannot be edited.");
        }

        SetDetails(title, description, price, condition, category, address, latitude, longitude, linkedRentalPostId,
            listingType, availableQuantity, unit, preparationMinutes);
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

        if (ReservedQuantity > 0)
        {
            throw new DomainException("Marketplace posts with active reservations cannot be archived.");
        }

        Status = MarketplacePostStatus.Archived;
        UpdatedAt = updatedAt;
    }

    public void Reserve(int quantity, DateTimeOffset updatedAt)
    {
        EnsureActive();
        if (quantity <= 0 || quantity > AvailableQuantity)
        {
            throw new DomainException("Requested quantity exceeds available marketplace stock.");
        }

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
        UpdatedAt = updatedAt;
    }

    public void ReleaseReservation(int quantity, DateTimeOffset updatedAt)
    {
        if (quantity <= 0 || quantity > ReservedQuantity)
        {
            throw new DomainException("Marketplace reservation quantity is invalid.");
        }

        ReservedQuantity -= quantity;
        AvailableQuantity += quantity;
        UpdatedAt = updatedAt;
    }

    public void CompleteReservation(int quantity, DateTimeOffset updatedAt)
    {
        if (quantity <= 0 || quantity > ReservedQuantity)
        {
            throw new DomainException("Marketplace completion quantity is invalid.");
        }

        ReservedQuantity -= quantity;
        if (ListingType == MarketplaceListingType.SecondHand)
        {
            Status = MarketplacePostStatus.Sold;
        }

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
        Guid? linkedRentalPostId,
        MarketplaceListingType listingType,
        int availableQuantity,
        string unit,
        int? preparationMinutes)
    {
        Title = NormalizeRequired(title, MaxTitleLength, nameof(Title));
        Description = NormalizeRequired(description, MaxDescriptionLength, nameof(Description));
        Price = price > 0 ? price : throw new DomainException("Giá sản phẩm phải lớn hơn 0.");
        Condition = NormalizeRequired(condition, MaxConditionLength, nameof(Condition));
        Category = NormalizeRequired(category, MaxCategoryLength, nameof(Category));
        Address = NormalizeRequired(address, MaxAddressLength, nameof(Address));
        Latitude = ValidateCoordinate(latitude, -90, 90, nameof(Latitude));
        Longitude = ValidateCoordinate(longitude, -180, 180, nameof(Longitude));
        LinkedRentalPostId = linkedRentalPostId == Guid.Empty ? null : linkedRentalPostId;
        if (!Enum.IsDefined(listingType))
        {
            throw new DomainException("Marketplace listing type is invalid.");
        }

        var maxStock = listingType == MarketplaceListingType.Food ? MaxFoodStock : 1;
        if (availableQuantity is < 1 || availableQuantity > maxStock)
        {
            throw new DomainException($"Available quantity must be between 1 and {maxStock}.");
        }

        if (preparationMinutes is < 0 or > 240)
        {
            throw new DomainException("Preparation time must be between 0 and 240 minutes.");
        }

        ListingType = listingType;
        AvailableQuantity = availableQuantity;
        Unit = NormalizeRequired(unit, MaxUnitLength, nameof(Unit));
        PreparationMinutes = listingType == MarketplaceListingType.Food ? preparationMinutes : null;
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
            throw new DomainException($"Tin chợ đồ cần từ 1 đến {MaxMediaCount} ảnh.");
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
            throw new DomainException("Chỉ tin chợ đồ đang bán mới được sửa.");
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} là bắt buộc.");
        }

        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
    }

    private static decimal ValidateCoordinate(decimal value, decimal min, decimal max, string fieldName)
    {
        return value >= min && value <= max
            ? value
            : throw new DomainException($"{fieldName} nằm ngoài phạm vi cho phép.");
    }
}
