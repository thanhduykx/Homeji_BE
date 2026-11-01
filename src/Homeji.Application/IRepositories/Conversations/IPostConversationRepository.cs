using Homeji.Application.DTOs.Conversations;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Conversations;

public interface IPostConversationRepository
{
    Task<PostConversation?> FindAsync(ConversationSubjectType subjectType, Guid subjectId, Guid participantAId, Guid participantBId, CancellationToken cancellationToken = default);
    Task<PostConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostConversation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserMessageBlock>> GetUserBlocksAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsMessagingBlockedAsync(Guid userId, Guid otherId, CancellationToken cancellationToken = default);
    Task SetUserBlockAsync(Guid userId, Guid otherId, bool blocked, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<PostMessageAttachment?> GetAttachmentAsync(Guid conversationId, Guid messageId, Guid attachmentId, CancellationToken cancellationToken = default);
    Task<int> CountAttachmentsByUploaderSinceAsync(Guid uploaderId, DateTimeOffset since, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, ConversationLastMessageDto>> GetLatestByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> CountUnreadByConversationIdsAsync(
        Guid userId,
        IReadOnlyCollection<PostConversation> conversations,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> CountBySubjectsAsync(ConversationSubjectType subjectType, IReadOnlyCollection<Guid> subjectIds, CancellationToken cancellationToken = default);
    Task AddConversationAsync(PostConversation conversation, CancellationToken cancellationToken = default);
    Task AddMessageAsync(PostMessage message, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
