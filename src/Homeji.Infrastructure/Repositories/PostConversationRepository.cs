using Homeji.Application.IRepositories.Conversations;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class PostConversationRepository : IPostConversationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public PostConversationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PostConversation?> FindAsync(
        ConversationSubjectType subjectType,
        Guid subjectId,
        Guid participantAId,
        Guid participantBId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PostConversations.SingleOrDefaultAsync(conversation =>
            conversation.SubjectType == subjectType
            && conversation.SubjectId == subjectId
            && ((conversation.ParticipantAId == participantAId && conversation.ParticipantBId == participantBId)
                || (conversation.ParticipantAId == participantBId && conversation.ParticipantBId == participantAId)),
            cancellationToken);
    }

    public Task<PostConversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PostConversations.SingleOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<PostConversation>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PostConversations
            .AsNoTracking()
            .Where(conversation => conversation.ParticipantAId == userId || conversation.ParticipantBId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PostMessage>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.PostMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountBySubjectsAsync(
        ConversationSubjectType subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken = default)
    {
        if (subjectIds.Count == 0) return new Dictionary<Guid, int>();
        return await _dbContext.PostConversations.AsNoTracking()
            .Where(conversation => conversation.SubjectType == subjectType && subjectIds.Contains(conversation.SubjectId))
            .GroupBy(conversation => conversation.SubjectId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);
    }

    public async Task AddConversationAsync(PostConversation conversation, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostConversations.AddAsync(conversation, cancellationToken);
    }

    public async Task AddMessageAsync(PostMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.PostMessages.AddAsync(message, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
