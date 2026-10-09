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

    public Task<RoommateInvitation?> GetActiveIndependentAsync(Guid firstUserId, Guid secondUserId, CancellationToken cancellationToken = default) =>
        _dbContext.RoommateInvitations.AsNoTracking().SingleOrDefaultAsync(x => x.RentalPostId == null
            && (x.Status == RoommateInvitationStatus.Pending || x.Status == RoommateInvitationStatus.Accepted)
            && ((x.SenderId == firstUserId && x.ReceiverId == secondUserId) || (x.SenderId == secondUserId && x.ReceiverId == firstUserId)), cancellationToken);

    public Task<bool> HasPendingAsync(Guid? rentalPostId, Guid senderId, Guid receiverId, CancellationToken cancellationToken = default)
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

    public async Task<IReadOnlyList<RoommateInvitation>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0) return [];

        return await _dbContext.RoommateInvitations
            .AsNoTracking()
            .Where(invitation => ids.Contains(invitation.Id))
            .ToListAsync(cancellationToken);
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try { await _dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: "ux_independent_roommate_connection" })
        {
            foreach (var entry in _dbContext.ChangeTracker.Entries().Where(x => x.State == EntityState.Added
                && (x.Entity is RoommateInvitation || x.Entity is Notification)).ToArray()) entry.State = EntityState.Detached;
            throw new Homeji.Application.Common.Exceptions.RequestValidationException(new Dictionary<string, string[]>
            { ["receiverId"] = ["Đã có kết nối ở ghép giữa hai người. Vui lòng tải lại lời mời."] });
        }
    }
}
