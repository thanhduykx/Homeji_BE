using Homeji.Application.IRepositories.RoommateChats;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RoommateConversationRepository : IRoommateConversationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RoommateConversationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RoommateConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.RoommateConversations.SingleOrDefaultAsync(
            conversation => conversation.Id == id,
            cancellationToken);
    }

    public Task<RoommateConversation?> GetByInvitationIdAsync(
        Guid invitationId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RoommateConversations.SingleOrDefaultAsync(
            conversation => conversation.InvitationId == invitationId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RoommateConversation>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RoommateConversations
            .AsNoTracking()
            .Where(conversation =>
                conversation.FirstParticipantId == userId
                || conversation.SecondParticipantId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoommateMessage>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RoommateMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddConversationAsync(
        RoommateConversation conversation,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.RoommateConversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddMessageAsync(RoommateMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.RoommateMessages.AddAsync(message, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
