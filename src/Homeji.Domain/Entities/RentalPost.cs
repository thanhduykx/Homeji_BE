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
    public const int MaxTransferReasonLength = 500;
    public const int MaxOwnerConsentContactLength = 200;
    public const int MaxOwnerConsentVerificationNoteLength = 500;
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
    public RoomTransferKind? TransferKind { get; private set; }
    public DateOnly? OriginalLeaseEndsOn { get; private set; }
    public decimal PassFee { get; private set; }
    public string? TransferReason { get; private set; }
    public bool OwnerConsentConfirmed { get; private set; }
    public string? OwnerConsentContact { get; private set; }
    public DateTimeOffset? OwnerConsentVerifiedAt { get; private set; }
    public Guid? OwnerConsentVerifiedBy { get; private set; }
    public string? OwnerConsentVerificationNote { get; private set; }

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
            throw new DomainException("Mã chủ tin không được để trống.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Loại tin đăng không hợp lệ.");
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
        DateOnly? availableFrom = null,
        RoomTransferKind? transferKind = null,
        DateOnly? originalLeaseEndsOn = null,
        decimal passFee = 0,
        string? transferReason = null,
        bool ownerConsentConfirmed = false,
        string? ownerConsentContact = null)
    {
        EnsureEditable();
        var wasActive = Status == RentalPostStatus.Active;
        Type = type;
        Title = NormalizeRequired(title, MaxTitleLength, nameof(Title));
        Description = NormalizeRequired(description, MaxDescriptionLength, nameof(Description));
        Price = RequirePositive(price, nameof(Price));
        Deposit = deposit < 0 ? throw new DomainException("Tiền cọc không được âm.") : deposit;
        Area = RequirePositive(area, nameof(Area));
        ElectricityPrice = RequireNonNegative(electricityPrice, nameof(ElectricityPrice));
        WaterPrice = RequireNonNegative(waterPrice, nameof(WaterPrice));
        InternetPrice = RequireNonNegative(internetPrice, nameof(InternetPrice));
        if (maxOccupants <= 0 || availableSlots <= 0 || availableSlots > maxOccupants)
        {
            throw new DomainException("Số chỗ trống phải từ 1 đến số người tối đa.");
        }

        MaxOccupants = maxOccupants;
        AvailableSlots = availableSlots;
        HouseRules = NormalizeOptional(houseRules, MaxHouseRulesLength, nameof(HouseRules));
        AvailableFrom = availableFrom;
        if (type == RentalPostType.RoomTransfer)
        {
            if (transferKind is null || !Enum.IsDefined(transferKind.Value))
            {
                throw new DomainException("Room transfer kind is required.");
            }

            if (originalLeaseEndsOn is null || availableFrom is null || originalLeaseEndsOn <= availableFrom)
            {
                throw new DomainException("Original lease end date must be after the available date.");
            }

            TransferKind = transferKind;
            OriginalLeaseEndsOn = originalLeaseEndsOn;
            PassFee = RequireNonNegative(passFee, nameof(PassFee));
            TransferReason = NormalizeRequired(transferReason!, MaxTransferReasonLength, nameof(TransferReason));
            OwnerConsentConfirmed = ownerConsentConfirmed;
            OwnerConsentContact = NormalizeRequired(
                ownerConsentContact!,
                MaxOwnerConsentContactLength,
                nameof(OwnerConsentContact));
            OwnerConsentVerifiedAt = null;
            OwnerConsentVerifiedBy = null;
            OwnerConsentVerificationNote = null;
        }
        else
        {
            TransferKind = null;
            OriginalLeaseEndsOn = null;
            PassFee = 0;
            TransferReason = null;
            OwnerConsentConfirmed = false;
            OwnerConsentContact = null;
            OwnerConsentVerifiedAt = null;
            OwnerConsentVerifiedBy = null;
            OwnerConsentVerificationNote = null;
        }
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
            throw new DomainException("Không tìm thấy ảnh/media trên tin đăng này.");
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
            throw new DomainException("Cần hoàn thiện thông tin tin đăng trước khi gửi duyệt.");
        }

        if (_media.Count(item => item.MediaType == MediaType.Image) < MinimumImageCountForSubmit)
        {
            throw new DomainException($"Tin đăng cần ít nhất {MinimumImageCountForSubmit} ảnh.");
        }

        if (Type == RentalPostType.RoomTransfer
            && (!OwnerConsentConfirmed
                || string.IsNullOrWhiteSpace(OwnerConsentContact)
                || TransferKind is null
                || OriginalLeaseEndsOn is null))
        {
            throw new DomainException("Room transfer requires owner consent details before submission.");
        }

        Status = RentalPostStatus.Pending;
        ModerationReason = null;
        UpdatedAt = submittedAt;
    }

    public void Approve(DateTimeOffset approvedAt, Guid? reviewerId = null, string? ownerConsentVerificationNote = null)
    {
        if (Status != RentalPostStatus.Pending)
        {
            throw new DomainException("Chỉ tin đang chờ duyệt mới được phê duyệt.");
        }

        if (Type == RentalPostType.RoomTransfer)
        {
            if (reviewerId is null || reviewerId == Guid.Empty)
            {
                throw new DomainException("Room transfer approval requires a reviewer.");
            }

            OwnerConsentVerifiedAt = approvedAt;
            OwnerConsentVerifiedBy = reviewerId;
            OwnerConsentVerificationNote = NormalizeRequired(
                ownerConsentVerificationNote!,
                MaxOwnerConsentVerificationNoteLength,
                nameof(ownerConsentVerificationNote));
        }
        Status = RentalPostStatus.Active;
        ModerationReason = null;
        UpdatedAt = approvedAt;
    }

    public void Reject(string reason, DateTimeOffset rejectedAt)
    {
        if (Status != RentalPostStatus.Pending)
        {
            throw new DomainException("Chỉ tin đang chờ duyệt mới bị từ chối.");
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
            throw new DomainException("Chỉ tin đang hoạt động mới đánh dấu đã thuê.");
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
            throw new DomainException("Tin đăng không thể chỉnh sửa ở trạng thái hiện tại.");
        }
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} là bắt buộc.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }

    private static string NormalizeAmenity(string code)
    {
        return NormalizeRequired(code, MaxAmenityCodeLength, nameof(code)).ToUpperInvariant();
    }

    private static decimal RequirePositive(decimal value, string fieldName)
    {
        return value <= 0 ? throw new DomainException($"{fieldName} phải lớn hơn 0.") : value;
    }

    private static decimal RequireNonNegative(decimal value, string fieldName)
    {
        return value < 0 ? throw new DomainException($"{fieldName} không được âm.") : value;
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
            : throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
    }

    private static decimal ValidateCoordinate(decimal value, decimal min, decimal max, string fieldName)
    {
        return value < min || value > max
            ? throw new DomainException($"{fieldName} nằm ngoài phạm vi cho phép.")
            : value;
    }
}
