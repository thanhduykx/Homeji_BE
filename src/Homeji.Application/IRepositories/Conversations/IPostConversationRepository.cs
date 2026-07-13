using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Conversations;

public interface IPostConversationRepository
{
    Task<PostConversation?> FindAsync(ConversationSubjectType subjectType, Guid subjectId, Guid participantAId, Guid participantBId, CancellationToken cancellationToken = default);
    Task<PostConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostConversation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> CountBySubjectsAsync(ConversationSubjectType subjectType, IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken = default);
    Task AddConversationAsync(PostConversation conversation, CancellationToken cancellationToken = default);
    Task AddMessageAsync(PostMessage message, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
