using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.MarketplaceOrders;

public sealed record CreateMarketplaceOrderDto(DateTimeOffset PickupAt, string? PickupAddress, string? Note);

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
    DateTimeOffset UpdatedAt);
