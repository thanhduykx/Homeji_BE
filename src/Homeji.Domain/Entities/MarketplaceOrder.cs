using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class MarketplaceOrder
{
    public const int MaxPickupAddressLength = 500;
    public const int MaxNoteLength = 500;

    private MarketplaceOrder()
    {
        PickupAddress = null!;
    }

    public MarketplaceOrder(
        Guid marketplacePostId,
        Guid buyerId,
        Guid sellerId,
        decimal agreedPrice,
        DateTimeOffset pickupAt,
        string pickupAddress,
        string? note,
        DateTimeOffset createdAt,
        int quantity = 1,
        decimal platformFeeRate = 0.10m,
        Guid? checkoutId = null,
        MarketplaceFulfillmentType fulfillmentType = MarketplaceFulfillmentType.Pickup,
        MarketplaceDelivery? delivery = null)
    {
        if (buyerId == sellerId)
        {
            throw new DomainException("Người bán không thể mua sản phẩm của chính mình.");
        }

        if (pickupAt <= createdAt)
        {
            throw new DomainException("Thời gian nhận hàng phải ở tương lai.");
        }

        Id = Guid.NewGuid();
        CheckoutId = checkoutId ?? Id;
        if (CheckoutId == Guid.Empty)
        {
            throw new DomainException("Mã thanh toán không được để trống.");
        }

        SetFulfillment(fulfillmentType, delivery);
        MarketplacePostId = marketplacePostId;
        BuyerId = buyerId;
        SellerId = sellerId;
        if (agreedPrice <= 0 || quantity <= 0 || platformFeeRate is <= 0 or >= 1)
        {
            throw new DomainException("Order price, quantity, or platform fee rate is invalid.");
        }

        UnitPrice = agreedPrice;
        Quantity = quantity;
        AgreedPrice = agreedPrice * quantity;
        PlatformFeeRate = platformFeeRate;
        PlatformFeeAmount = decimal.Round(AgreedPrice * platformFeeRate, 0, MidpointRounding.AwayFromZero);
        SellerNetAmount = AgreedPrice - PlatformFeeAmount;
        PickupAt = pickupAt;
        PickupAddress = Normalize(pickupAddress, MaxPickupAddressLength, nameof(PickupAddress));
        Note = NormalizeOptional(note, MaxNoteLength, nameof(Note));
        Status = MarketplaceOrderStatus.Requested;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid CheckoutId { get; private set; }
    public MarketplaceFulfillmentType FulfillmentType { get; private set; }
    public string? RecipientName { get; private set; }
    public string? RecipientPhone { get; private set; }
    public string? DeliveryAddress { get; private set; }
    public decimal? DeliveryLatitude { get; private set; }
    public decimal? DeliveryLongitude { get; private set; }
    public Guid MarketplacePostId { get; private set; }
    public Guid BuyerId { get; private set; }
    public Guid SellerId { get; private set; }
    public decimal AgreedPrice { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal PlatformFeeRate { get; private set; }
    public decimal PlatformFeeAmount { get; private set; }
    public decimal SellerNetAmount { get; private set; }
    public DateTimeOffset? FundsReleasedAt { get; private set; }
    public DateTimeOffset? DeliveredAt { get; private set; }
    public DateTimeOffset? RefundedAt { get; private set; }
    public DateTimeOffset PickupAt { get; private set; }
    public string PickupAddress { get; private set; }
    public string? Note { get; private set; }
    public MarketplaceOrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Accept(DateTimeOffset updatedAt) => Transition(MarketplaceOrderStatus.Requested, MarketplaceOrderStatus.Accepted, updatedAt);
    public void Reject(DateTimeOffset updatedAt) => Transition(MarketplaceOrderStatus.Requested, MarketplaceOrderStatus.Rejected, updatedAt);

    public void Cancel(DateTimeOffset updatedAt)
    {
        if (Status != MarketplaceOrderStatus.Requested)
        {
            throw new DomainException("Marketplace orders can only be cancelled before the seller accepts them.");
        }

        Status = MarketplaceOrderStatus.Cancelled;
        UpdatedAt = updatedAt;
    }

    public void Complete(DateTimeOffset updatedAt) => ConfirmReceived(updatedAt);

    public void MarkDelivered(DateTimeOffset updatedAt)
    {
        Transition(MarketplaceOrderStatus.Accepted, MarketplaceOrderStatus.Delivered, updatedAt);
        DeliveredAt = updatedAt;
    }

    public void ConfirmReceived(DateTimeOffset updatedAt) =>
        Transition(MarketplaceOrderStatus.Delivered, MarketplaceOrderStatus.Completed, updatedAt);

    public void AutoComplete(DateTimeOffset updatedAt)
    {
        if (Status == MarketplaceOrderStatus.Delivered)
        {
            Status = MarketplaceOrderStatus.Completed;
            UpdatedAt = updatedAt;
        }
        else if (Status != MarketplaceOrderStatus.Completed)
        {
            throw new DomainException("Marketplace order cannot be auto-completed in the current state.");
        }
    }

    public void Expire(DateTimeOffset updatedAt)
    {
        Transition(MarketplaceOrderStatus.Requested, MarketplaceOrderStatus.Expired, updatedAt);
    }

    public void MarkFundsReleased(DateTimeOffset updatedAt)
    {
        if (Status != MarketplaceOrderStatus.Completed
            || !DeliveredAt.HasValue
            || updatedAt < DeliveredAt.Value
            || FundsReleasedAt.HasValue
            || RefundedAt.HasValue)
        {
            throw new DomainException("Marketplace order funds cannot be released in the current state.");
        }

        FundsReleasedAt = updatedAt;
        UpdatedAt = updatedAt;
    }

    public void MarkRefunded(DateTimeOffset updatedAt)
    {
        if (Status is not (MarketplaceOrderStatus.Rejected or MarketplaceOrderStatus.Cancelled or MarketplaceOrderStatus.Expired)
            || FundsReleasedAt.HasValue
            || RefundedAt.HasValue)
        {
            throw new DomainException("Marketplace order cannot be refunded in the current state.");
        }

        RefundedAt = updatedAt;
        UpdatedAt = updatedAt;
    }

    private void Transition(MarketplaceOrderStatus expected, MarketplaceOrderStatus target, DateTimeOffset updatedAt)
    {
        if (Status != expected)
        {
            throw new DomainException($"Chỉ đơn chợ đồ ở trạng thái {expected} mới chuyển sang {target}.");
        }

        Status = target;
        UpdatedAt = updatedAt;
    }

    private void SetFulfillment(MarketplaceFulfillmentType fulfillmentType, MarketplaceDelivery? delivery)
    {
        if (!Enum.IsDefined(fulfillmentType)
            || (fulfillmentType == MarketplaceFulfillmentType.SellerDelivery && delivery is null)
            || (fulfillmentType == MarketplaceFulfillmentType.Pickup && delivery is not null))
        {
            throw new DomainException("Thông tin nhận hàng không hợp lệ.");
        }

        FulfillmentType = fulfillmentType;
        if (delivery is null) return;
        RecipientName = Normalize(delivery.RecipientName, 100, nameof(RecipientName));
        RecipientPhone = Normalize(delivery.RecipientPhone, 16, nameof(RecipientPhone));
        var digits = RecipientPhone.StartsWith('+') ? RecipientPhone[1..] : RecipientPhone;
        if (digits.Length is < 9 or > 15 || digits.Any(character => character is < '0' or > '9')
            || delivery.Latitude is < -90 or > 90 || delivery.Longitude is < -180 or > 180)
        {
            throw new DomainException("Số điện thoại hoặc tọa độ giao hàng không hợp lệ.");
        }

        DeliveryAddress = Normalize(delivery.Address, MaxPickupAddressLength, nameof(DeliveryAddress));
        DeliveryLatitude = delivery.Latitude;
        DeliveryLongitude = delivery.Longitude;
    }

    private static string Normalize(string value, int maxLength, string fieldName)
    {
        return NormalizeOptional(value, maxLength, fieldName)
            ?? throw new DomainException($"{fieldName} là bắt buộc.");
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
    }
}
