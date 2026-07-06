using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.SavedPosts;

public interface ISavedPostRepository
{
    Task<bool> ExistsAsync(Guid userId, Guid rentalPostId, CancellationToken cancellationToken = default);

    Task AddAsync(SavedPost savedPost, CancellationToken cancellationToken = default);

    Task<bool> RemoveAsync(Guid userId, Guid rentalPostId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedPost>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedPost>> GetByPostAsync(Guid rentalPostId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
