using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalPostSummaryDto(
    Guid Id,
    RentalPostType Type,
    string Title,
    decimal Price,
    decimal Area,
    string Address,
    decimal Latitude,
    decimal Longitude,
    string? ThumbnailPath,
    int ViewCount,
    int SaveCount);
