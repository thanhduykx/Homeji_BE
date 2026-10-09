using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
namespace Homeji.Application.IRepositories.Roommates;
public interface IRoommateDirectoryRepository
{
    Task<RoommateProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SaveAsync(RoommateProfile profile, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<(UserProfile User, RoommateProfile Profile)> Items, int Total)> SearchAsync(
        Guid excludedUserId, RoommateIntent? intent, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default);
}
