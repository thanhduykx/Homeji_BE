using Homeji.Application.DTOs.Chatbot;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Chatbot;

public static class ChatbotMapper
{
    public static ChatbotConversationDto ToConversationDto(ChatConversation conversation)
    {
        var lastMessage = conversation.Messages
            .OrderByDescending(message => message.CreatedAt)
            .FirstOrDefault();

        return new ChatbotConversationDto(
            conversation.Id,
            conversation.Title,
            lastMessage?.Content,
            conversation.CreatedAt,
            conversation.UpdatedAt);
    }

    public static ChatbotMessageDto ToMessageDto(ChatMessage message)
    {
        return new ChatbotMessageDto(
            message.Id,
            message.ConversationId,
            message.Sender,
            message.Content,
            message.CreatedAt);
    }
}
