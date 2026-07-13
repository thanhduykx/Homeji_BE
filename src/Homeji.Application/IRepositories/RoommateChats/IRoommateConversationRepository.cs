using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.RoommateChats;

public interface IRoommateConversationRepository
{
    Task<RoommateConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RoommateConversation?> GetByInvitationIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoommateConversation>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoommateMessage>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task AddConversationAsync(
        RoommateConversation conversation,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(RoommateMessage message, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
