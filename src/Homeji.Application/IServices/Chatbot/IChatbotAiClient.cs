using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.IServices.Chatbot;

public interface IChatbotAiClient
{
    Task<string> GeneratePostDraftAsync(string prompt, IReadOnlyCollection<ChatbotMediaContent> images,
        CancellationToken cancellationToken = default) => GenerateMediaReplyAsync([], prompt, images, cancellationToken);
    Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> conversationMessages,
        string latestUserMessage, IReadOnlyCollection<ChatbotMediaContent> attachments,
        CancellationToken cancellationToken = default);
    Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default);

    Task<string> GenerateReplyAsync(
        IReadOnlyCollection<ChatbotMessageDto> conversationMessages,
        string latestUserMessage,
        CancellationToken cancellationToken = default);
}
