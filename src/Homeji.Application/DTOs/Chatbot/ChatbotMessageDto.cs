using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotMessageDto(
    Guid Id,
    Guid ConversationId,
    ChatMessageSender Sender,
    string Content,
    DateTimeOffset CreatedAt);
