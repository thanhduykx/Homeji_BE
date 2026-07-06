using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record RentalPostDto(
    Guid Id,
    Guid OwnerId,
    RentalPostType Type,
    RentalPostStatus Status,
    string Title,
    string Description,
    decimal Price,
    decimal Deposit,
    decimal Area,
    string Address,
    decimal Latitude,
    decimal Longitude,
    IReadOnlyCollection<string> Amenities,
    IReadOnlyCollection<RentalPostMediaDto> Media,
    int ViewCount,
    int SaveCount,
    string? ModerationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
