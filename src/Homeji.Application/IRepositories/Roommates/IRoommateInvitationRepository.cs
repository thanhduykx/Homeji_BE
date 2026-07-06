using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Roommates;

public interface IRoommateInvitationRepository
{
    Task<bool> HasPendingAsync(
        Guid rentalPostId,
        Guid senderId,
        Guid receiverId,
        CancellationToken cancellationToken = default);

    Task<RoommateInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoommateInvitation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(RoommateInvitation invitation, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
