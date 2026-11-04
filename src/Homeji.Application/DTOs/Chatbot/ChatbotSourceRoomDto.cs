namespace Homeji.Application.DTOs.Chatbot;

/// <summary>A public source listing matched from chat criteria. Never carries contact numbers.</summary>
public sealed record ChatbotSourceRoomDto(
    Guid Id,
    string Title,
    string Address,
    decimal Price,
    decimal Area,
    IReadOnlyList<string> Amenities,
    string? DepositLabel,
    string? Excerpt);

public sealed record ChatbotSourceMatchDto(IReadOnlyList<ChatbotSourceRoomDto> Rooms, int Total);
