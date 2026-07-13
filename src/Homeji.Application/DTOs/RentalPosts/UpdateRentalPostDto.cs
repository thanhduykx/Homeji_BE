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
    IReadOnlyCollection<string> Amenities,
    decimal ElectricityPrice = 0,
    decimal WaterPrice = 0,
    decimal InternetPrice = 0,
    int MaxOccupants = 1,
    int AvailableSlots = 1,
    string? HouseRules = null,
    DateOnly? AvailableFrom = null);
