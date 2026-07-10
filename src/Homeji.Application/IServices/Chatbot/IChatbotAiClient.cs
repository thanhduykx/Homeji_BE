using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.IServices.Chatbot;

public interface IChatbotAiClient
{
    Task<string> GenerateReplyAsync(
        IReadOnlyCollection<ChatbotMessageDto> conversationMessages,
        string latestUserMessage,
        CancellationToken cancellationToken = default);
}
