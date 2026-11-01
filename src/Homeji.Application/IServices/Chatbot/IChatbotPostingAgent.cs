using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.IServices.Chatbot;

public interface IChatbotPostingAgent
{
    Task<AgentPostResultDto> PrepareAsync(string message, IReadOnlyCollection<ChatbotMediaContent> media,
        Guid? previousDraftId, CancellationToken cancellationToken);
    Task<Guid> ConfirmAsync(Guid draftId, AgentPostDetails details, CancellationToken cancellationToken);
    Task CancelAsync(Guid draftId, CancellationToken cancellationToken);
}
