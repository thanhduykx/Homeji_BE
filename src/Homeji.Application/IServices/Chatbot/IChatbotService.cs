using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.IServices.Chatbot;

public interface IChatbotService
{
    Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<ChatbotPopupConfigDto> GetPopupConfigAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatbotConversationDto>> GetMyConversationsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatbotMessageDto>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<ChatbotReplyDto> SendMessageAsync(
        SendChatbotMessageDto request,
        CancellationToken cancellationToken = default);
}
