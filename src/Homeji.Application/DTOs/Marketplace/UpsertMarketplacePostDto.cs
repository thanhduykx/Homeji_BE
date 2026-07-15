namespace Homeji.Application.DTOs.Marketplace;

public sealed record UpsertMarketplacePostDto(
    string? Title,
    string? Description,
    decimal Price,
    string? Condition,
    string? Category,
    string? Address,
    decimal Latitude,
    decimal Longitude,
    Guid? LinkedRentalPostId,
    IReadOnlyCollection<string> MediaUrls,
    Homeji.Domain.Enums.MarketplaceListingType ListingType = Homeji.Domain.Enums.MarketplaceListingType.SecondHand,
    int AvailableQuantity = 1,
    string? Unit = "món",
    int? PreparationMinutes = null);
