using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Chatbot;

public interface IChatConversationRepository
{
    Task<ChatConversation?> GetByIdWithMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChatConversation>> GetByUserIdAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
