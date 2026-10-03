using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.MarketplaceOrders;

public sealed record CreateMarketplaceOrderDto(
    DateTimeOffset PickupAt, string? PickupAddress, string? Note, int Quantity = 1,
    MarketplaceFulfillmentDto? Fulfillment = null);

public sealed record MarketplaceFulfillmentDto(
    MarketplaceFulfillmentType Mode,
    MarketplaceDeliveryDto? Delivery = null);

public sealed record MarketplaceDeliveryDto(
    string? RecipientName, string? RecipientPhone, string? Address, decimal Latitude, decimal Longitude);

public sealed record MarketplaceCartItemDto(Guid PostId, int Quantity);

public sealed record CreateMarketplaceCartOrderDto(
    IReadOnlyList<MarketplaceCartItemDto> Items,
    DateTimeOffset PickupAt,
    string? PickupAddress,
    string? Note,
    MarketplaceFulfillmentDto? Fulfillment = null);

public sealed record MarketplaceOrderDto(
    Guid Id,
    Guid MarketplacePostId,
    Guid BuyerId,
    Guid SellerId,
    decimal AgreedPrice,
    DateTimeOffset PickupAt,
    string PickupAddress,
    string? Note,
    MarketplaceOrderStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    decimal UnitPrice,
    int Quantity,
    decimal PlatformFeeRate,
    decimal PlatformFeeAmount,
    decimal SellerNetAmount,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? FundsReleaseDueAt,
    DateTimeOffset? FundsReleasedAt,
    DateTimeOffset? RefundedAt,
    string? PostTitle = null,
    string? PostImageUrl = null,
    string? BuyerDisplayName = null,
    string? SellerDisplayName = null,
    string? SellerAddress = null,
    Guid CheckoutId = default,
    MarketplaceFulfillmentType FulfillmentType = MarketplaceFulfillmentType.Pickup,
    MarketplaceDeliveryDto? Delivery = null);
