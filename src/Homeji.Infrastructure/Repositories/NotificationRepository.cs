using Homeji.Application.IRepositories.Notifications;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public NotificationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.SingleOrDefaultAsync(notification => notification.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, bool unreadOnly, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications.Where(notification => notification.RecipientId == userId);
        if (unreadOnly)
        {
            query = query.Where(notification => !notification.IsRead);
        }

        return await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
    }

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddRangeAsync(notifications, cancellationToken);
    }

    public async Task AddRangeAndSaveAsync(
        IEnumerable<Notification> notifications,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddRangeAsync(notifications, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _dbContext.ChangeTracker.Clear();
    }

    public async Task MarkDirectMessagesReadAsync(
        Guid userId,
        Guid conversationId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default)
    {
        var notifications = await _dbContext.Notifications
            .Where(notification =>
                notification.RecipientId == userId
                && !notification.IsRead
                && notification.RelatedEntityId == conversationId
                && (notification.Type == Domain.Enums.NotificationType.DirectMessage
                    || notification.Type == Domain.Enums.NotificationType.NewMessage))
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.MarkRead(readAt);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
