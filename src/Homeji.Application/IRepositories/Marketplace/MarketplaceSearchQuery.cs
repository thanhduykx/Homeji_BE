using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Marketplace;

public sealed record MarketplaceSearchQuery(
    string? Keyword,
    string? Category,
    MarketplaceListingType? ListingType,
    decimal? MinPrice,
    decimal? MaxPrice,
    decimal? CenterLatitude,
    decimal? CenterLongitude,
    decimal? RadiusKm,
    decimal? MinLatitude,
    decimal? MaxLatitude,
    decimal? MinLongitude,
    decimal? MaxLongitude,
    int Skip,
    int Take,
    Guid? SellerId);
