using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Activities;

public interface IUserActivityRepository
{
    Task AddAsync(UserActivity activity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserActivity>> GetForUserAsync(Guid userId, UserActivityType? type, int take, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLatestByUserSinceAsync(DateTimeOffset since, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
