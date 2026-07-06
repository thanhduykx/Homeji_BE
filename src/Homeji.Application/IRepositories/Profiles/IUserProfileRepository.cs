using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Profiles;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfile> UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default);

    Task<UserProfile> SaveAsync(UserProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default);
}
