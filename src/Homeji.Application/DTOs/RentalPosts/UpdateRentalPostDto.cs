using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.RentalPosts;

public sealed record UpdateRentalPostDto(
    RentalPostType Type,
    string? Title,
    string? Description,
    decimal Price,
    decimal Deposit,
    decimal Area,
    string? Address,
    decimal Latitude,
    decimal Longitude,
    IReadOnlyCollection<string> Amenities);
