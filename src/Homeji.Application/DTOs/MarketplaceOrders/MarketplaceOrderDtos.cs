using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.MarketplaceOrders;

public sealed record CreateMarketplaceOrderDto(DateTimeOffset PickupAt, string? PickupAddress, string? Note, int Quantity = 1);

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
    DateTimeOffset? FundsReleasedAt,
    DateTimeOffset? RefundedAt,
    string? PostTitle = null,
    string? PostImageUrl = null,
    string? BuyerDisplayName = null,
    string? SellerDisplayName = null,
    string? SellerAddress = null);
