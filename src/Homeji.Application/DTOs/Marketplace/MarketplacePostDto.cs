using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Marketplace;

public sealed record MarketplacePostDto(
    Guid Id,
    Guid SellerId,
    string SellerDisplayName,
    string? SellerPhone,
    MarketplacePostStatus Status,
    string Title,
    string Description,
    decimal Price,
    string Condition,
    string Category,
    string Address,
    decimal Latitude,
    decimal Longitude,
    Guid? LinkedRentalPostId,
    IReadOnlyList<string> MediaUrls,
    decimal? DistanceKm,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
