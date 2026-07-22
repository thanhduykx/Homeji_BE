using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.WantedPosts;

public interface IRentalWantedPostRepository
{
    Task<RentalWantedPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalWantedPost>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<RentalWantedPost>>([]);
    Task<IReadOnlyList<RentalWantedPost>> SearchActiveAsync(string? area, decimal? maxBudget, int skip, int take, CancellationToken cancellationToken = default);
    Task AddAsync(RentalWantedPost post, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
