namespace Homeji.Application.DTOs.AI;

public sealed record RentalDraftRequestDto(string? TypeLabel, string? Address, string? Rent,
    string? Area, string[]? Amenities, int ImageCount, string? PriceBasis = null);
public sealed record RentalDraftResponseDto(string Title, string Description, string[] Missing);
