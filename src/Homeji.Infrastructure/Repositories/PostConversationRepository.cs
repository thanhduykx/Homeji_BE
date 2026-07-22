using Homeji.Application.DTOs.Conversations;
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
            .Include(message => message.Attachments)
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt)
            .ToListAsync(cancellationToken);
    }

    public Task<PostMessageAttachment?> GetAttachmentAsync(
        Guid conversationId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        return (
            from attachment in _dbContext.PostMessageAttachments
            join message in _dbContext.PostMessages on attachment.MessageId equals message.Id
            where message.ConversationId == conversationId
                && attachment.MessageId == messageId
                && attachment.Id == attachmentId
            select attachment).SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int> CountAttachmentsByUploaderSinceAsync(
        Guid uploaderId,
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.PostMessageAttachments.CountAsync(
            attachment => attachment.UploaderId == uploaderId && attachment.CreatedAt >= since,
            cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ConversationLastMessageDto>> GetLatestByConversationIdsAsync(
        IReadOnlyCollection<Guid> conversationIds,
        CancellationToken cancellationToken = default)
    {
        if (conversationIds.Count == 0)
        {
            return new Dictionary<Guid, ConversationLastMessageDto>();
        }

        var latest = await _dbContext.PostMessages
            .AsNoTracking()
            .Where(message => conversationIds.Contains(message.ConversationId))
            .GroupBy(message => message.ConversationId)
            .Select(group => group.OrderByDescending(message => message.SentAt).First())
            .ToListAsync(cancellationToken);

        return latest.ToDictionary(
            message => message.ConversationId,
            message => new ConversationLastMessageDto(
                message.ConversationId,
                message.Body,
                message.SenderId,
                message.SentAt));
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountUnreadByConversationIdsAsync(
        Guid userId,
        IReadOnlyCollection<PostConversation> conversations,
        CancellationToken cancellationToken = default)
    {
        if (conversations.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var conversationIds = conversations.Select(conversation => conversation.Id).ToArray();
        var incoming = await _dbContext.PostMessages
            .AsNoTracking()
            .Where(message =>
                conversationIds.Contains(message.ConversationId)
                && message.SenderId != userId)
            .Select(message => new { message.ConversationId, message.SentAt })
            .ToListAsync(cancellationToken);

        var lastReadByConversation = conversations.ToDictionary(
            conversation => conversation.Id,
            conversation => conversation.GetLastReadAt(userId) ?? DateTimeOffset.MinValue);

        return incoming
            .GroupBy(message => message.ConversationId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var cutoff = lastReadByConversation.GetValueOrDefault(group.Key, DateTimeOffset.MinValue);
                    return group.Count(message => message.SentAt > cutoff);
                });
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
