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
        DateTimeOffset createdAt)
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
        AgreedPrice = agreedPrice > 0 ? agreedPrice : throw new DomainException("Agreed price must be greater than zero.");
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
