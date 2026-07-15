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
        decimal platformFeeRate = 0.10m)
    {
        if (buyerId == sellerId)
        {
            throw new DomainException("Seller cannot buy their own marketplace item.");
        }

        if (pickupAt <= createdAt)
        {
            throw new DomainException("Pickup time must be in the future.");
        }

        Id = Guid.NewGuid();
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
        if (Status is not (MarketplaceOrderStatus.Requested or MarketplaceOrderStatus.Accepted))
        {
            throw new DomainException("This marketplace order can no longer be cancelled.");
        }

        Status = MarketplaceOrderStatus.Cancelled;
        UpdatedAt = updatedAt;
    }

    public void Complete(DateTimeOffset updatedAt) => Transition(MarketplaceOrderStatus.Accepted, MarketplaceOrderStatus.Completed, updatedAt);

    public void Expire(DateTimeOffset updatedAt)
    {
        Transition(MarketplaceOrderStatus.Requested, MarketplaceOrderStatus.Expired, updatedAt);
    }

    public void MarkFundsReleased(DateTimeOffset updatedAt)
    {
        if (Status != MarketplaceOrderStatus.Completed || FundsReleasedAt.HasValue || RefundedAt.HasValue)
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
            throw new DomainException($"Only {expected} marketplace orders can become {target}.");
        }

        Status = target;
        UpdatedAt = updatedAt;
    }

    private static string Normalize(string value, int maxLength, string fieldName)
    {
        return NormalizeOptional(value, maxLength, fieldName)
            ?? throw new DomainException($"{fieldName} is required.");
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
    }
}
