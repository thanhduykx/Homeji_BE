using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Notifications;

public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetForUserAsync(
        Guid userId,
        bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);

    async Task AddRangeAndSaveAsync(
        IEnumerable<Notification> notifications,
        CancellationToken cancellationToken = default)
    {
        await AddRangeAsync(notifications, cancellationToken);
        await SaveChangesAsync(cancellationToken);
    }

    Task MarkDirectMessagesReadAsync(
        Guid userId,
        Guid conversationId,
        DateTimeOffset readAt,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
