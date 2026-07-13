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
    IReadOnlyCollection<string> MediaUrls);
