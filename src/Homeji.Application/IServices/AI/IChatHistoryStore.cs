namespace Homeji.Application.IServices.AI;

public interface IChatHistoryStore
{
    Task DeleteOwnedAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken);
}
