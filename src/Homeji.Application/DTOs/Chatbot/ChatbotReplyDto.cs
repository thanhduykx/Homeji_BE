using Homeji.Application.DTOs.AI;

namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotReplyDto(
    Guid ConversationId,
    ChatbotMessageDto UserMessage,
    ChatbotMessageDto AssistantMessage,
    AiHighlightResponseDto? SearchUpdate);
