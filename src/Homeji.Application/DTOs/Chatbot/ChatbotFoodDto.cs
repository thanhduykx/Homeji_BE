namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotFoodContextDto(decimal Latitude, decimal Longitude, decimal RadiusKm = 3);

public sealed record ChatbotFoodItemDto(Guid Id, Guid SellerId, string SellerName, string Title,
    decimal Price, string Unit, int AvailableQuantity, int? PreparationMinutes,
    decimal? DistanceKm, DateTimeOffset UpdatedAt);

public sealed record ChatbotFoodResponseDto(string Status, string Summary,
    IReadOnlyList<ChatbotFoodItemDto> Items, string? Keyword = null, decimal? MaxPrice = null,
    ChatbotFoodContextDto? Origin = null);
