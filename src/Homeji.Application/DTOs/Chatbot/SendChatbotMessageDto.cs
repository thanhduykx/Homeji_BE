namespace Homeji.Application.DTOs.Chatbot;

public sealed record SendChatbotMessageDto(
    Guid? ConversationId,
    string? Message);
