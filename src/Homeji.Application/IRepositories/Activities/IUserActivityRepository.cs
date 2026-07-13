using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Activities;

public interface IUserActivityRepository
{
    Task AddAsync(UserActivity activity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserActivity>> GetForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
