using Homeji.Application.Common.Exceptions;
using Homeji.Application.IServices.AI;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class ChatHistoryStore(ApplicationDbContext dbContext) : IChatHistoryStore
{
    public async Task DeleteOwnedAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        // The ownership predicate is enforced in the DELETE; foreign IDs cannot be read or deleted.
        var deleted = await dbContext.ChatConversations.Where(conversation => conversation.Id == conversationId && conversation.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0) throw new NotFoundException("ChatConversation", conversationId);
    }
}
