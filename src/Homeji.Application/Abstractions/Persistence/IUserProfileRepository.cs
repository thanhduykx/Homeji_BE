using Homeji.Domain.Profiles;

namespace Homeji.Application.Abstractions.Persistence;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserProfile> UpsertAsync(UserProfile profile, CancellationToken cancellationToken = default);
}
