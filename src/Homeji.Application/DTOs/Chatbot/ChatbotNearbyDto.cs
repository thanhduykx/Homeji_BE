namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotNearbyItemDto(Guid Id, bool Imported, string Title, string Address, decimal Price, decimal Area, double DistanceKm);
public sealed record ChatbotNearbyDto(string Status, string Summary, IReadOnlyList<ChatbotNearbyItemDto> Items, decimal RadiusKm = 3);
