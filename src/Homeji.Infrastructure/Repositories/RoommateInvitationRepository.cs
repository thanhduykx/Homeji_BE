using Homeji.Application.IRepositories.Roommates;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RoommateInvitationRepository : IRoommateInvitationRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RoommateInvitationRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> HasPendingAsync(Guid rentalPostId, Guid senderId, Guid receiverId, CancellationToken cancellationToken = default)
    {
        return _dbContext.RoommateInvitations.AnyAsync(invitation =>
            invitation.RentalPostId == rentalPostId
            && invitation.Status == RoommateInvitationStatus.Pending
            && ((invitation.SenderId == senderId && invitation.ReceiverId == receiverId)
                || (invitation.SenderId == receiverId && invitation.ReceiverId == senderId)),
            cancellationToken);
    }

    public Task<RoommateInvitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.RoommateInvitations.SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RoommateInvitation>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.RoommateInvitations
            .AsNoTracking()
            .Where(invitation => invitation.SenderId == userId || invitation.ReceiverId == userId)
            .OrderByDescending(invitation => invitation.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RoommateInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _dbContext.RoommateInvitations.AddAsync(invitation, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
