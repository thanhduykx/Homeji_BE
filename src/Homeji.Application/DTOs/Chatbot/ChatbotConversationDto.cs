namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotConversationDto(
    Guid Id,
    string Title,
    string? LastMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
