using Homeji.Api.Views.Chatbot;
using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Api.Mappers;

public static class ChatbotViewMapper
{
    public static SendChatbotMessageDto ToDto(SendChatbotMessageViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        return new SendChatbotMessageDto(viewModel.ConversationId, viewModel.Message);
    }
}
