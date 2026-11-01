using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public async Task<IReadOnlyList<PostMessageDto>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await MessageHistoryQuery(conversationId).ToListAsync(cancellationToken);
    }

    internal IQueryable<PostMessageDto> MessageHistoryQuery(Guid conversationId) => _dbContext.PostMessages
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderBy(message => message.SentAt).ThenBy(message => message.Id)
            .Select(message => new PostMessageDto(message.Id, message.ConversationId, message.SenderId,
                message.Body, message.SentAt, message.Attachments.OrderBy(attachment => attachment.CreatedAt)
                .Select(attachment => new PostMessageAttachmentDto(attachment.Id, attachment.UploaderId,
                    attachment.Context, attachment.Status, attachment.MimeType, attachment.Bytes,
                    attachment.Width, attachment.Height,
                    "/api/conversations/" + message.ConversationId + "/messages/" + message.Id + "/attachments/" + attachment.Id + "/content",
                    attachment.CreatedAt, attachment.DeletedAt)).ToArray()));

    public async Task<IReadOnlyList<UserMessageBlock>> GetUserBlocksAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await _dbContext.UserMessageBlocks.AsNoTracking().Where(block => block.BlockerId == userId || block.BlockedId == userId).ToListAsync(cancellationToken);

    public Task<bool> IsMessagingBlockedAsync(Guid userId, Guid otherId, CancellationToken cancellationToken = default) =>
        _dbContext.UserMessageBlocks.AnyAsync(block => (block.BlockerId == userId && block.BlockedId == otherId)
            || (block.BlockerId == otherId && block.BlockedId == userId), cancellationToken);

    public async Task SetUserBlockAsync(Guid userId, Guid otherId, bool blocked, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        // Serialize a block change with message inserts for this pair across every thread.
        await _dbContext.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended(LEAST({userId}::text,{otherId}::text) || ':' || GREATEST({userId}::text,{otherId}::text), 724431))", cancellationToken);
        if (blocked)
            await _dbContext.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO homeji.user_message_blocks (\"BlockerId\",\"BlockedId\",\"CreatedAt\") VALUES ({userId},{otherId},{now}) ON CONFLICT (\"BlockerId\",\"BlockedId\") DO NOTHING", cancellationToken);
        else
            await _dbContext.UserMessageBlocks.Where(block => block.BlockerId == userId && block.BlockedId == otherId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
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
        return await UnreadCountsQuery(userId, conversationIds).ToDictionaryAsync(row => row.ConversationId, row => row.Count, cancellationToken);
    }

    internal sealed record UnreadCount(Guid ConversationId, int Count);
    internal IQueryable<UnreadCount> UnreadCountsQuery(Guid userId, Guid[] conversationIds) =>
        from message in _dbContext.PostMessages.AsNoTracking()
        join conversation in _dbContext.PostConversations on message.ConversationId equals conversation.Id
        where conversationIds.Contains(message.ConversationId) && message.SenderId != userId
            && ((conversation.ParticipantAId == userId && (conversation.ParticipantALastReadAt == null || message.SentAt > conversation.ParticipantALastReadAt))
                || (conversation.ParticipantBId == userId && (conversation.ParticipantBLastReadAt == null || message.SentAt > conversation.ParticipantBLastReadAt)))
        group message by message.ConversationId into messages
        select new UnreadCount(messages.Key, messages.Count());

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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { ConstraintName: "user_message_block" })
        {
            throw new ForbiddenAccessException("Không thể gửi tin nhắn vì một trong hai người đã chặn liên hệ.");
        }
    }
}
