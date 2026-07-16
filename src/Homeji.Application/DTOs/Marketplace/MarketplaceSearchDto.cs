namespace Homeji.Application.DTOs.Marketplace;

public sealed record MarketplaceSearchDto(
    string? Keyword,
    string? Category,
    Homeji.Domain.Enums.MarketplaceListingType? ListingType,
    decimal? MinPrice,
    decimal? MaxPrice,
    decimal? Latitude,
    decimal? Longitude,
    decimal? RadiusKm,
    Guid? NearRentalPostId,
    int Page,
    int PageSize);
