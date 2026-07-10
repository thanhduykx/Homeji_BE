using Homeji.Application.IRepositories.Chatbot;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class ChatConversationRepository : IChatConversationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ChatConversationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<ChatConversation?> GetByIdWithMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ChatConversations
            .Include(conversation => conversation.Messages)
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId, cancellationToken);
    }

    public async Task<IReadOnlyList<ChatConversation>> GetByUserIdAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(limit, 1, 50);

        return await _dbContext.ChatConversations
            .AsNoTracking()
            .Include(conversation => conversation.Messages)
            .Where(conversation => conversation.UserId == userId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ChatConversation conversation, CancellationToken cancellationToken = default)
    {
        await _dbContext.ChatConversations.AddAsync(conversation, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
