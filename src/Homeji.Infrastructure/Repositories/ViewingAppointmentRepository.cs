using Homeji.Application.IRepositories.Appointments;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class ViewingAppointmentRepository : IViewingAppointmentRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ViewingAppointmentRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(ViewingAppointment appointment, CancellationToken cancellationToken = default)
    {
        await _dbContext.ViewingAppointments.AddAsync(appointment, cancellationToken);
    }

    public Task<ViewingAppointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.ViewingAppointments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ViewingAppointment>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ViewingAppointments
            .AsNoTracking()
            .Where(item => item.RequesterId == userId || item.OwnerId == userId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasActiveAsync(
        Guid rentalPostId,
        Guid requesterId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.ViewingAppointments.AnyAsync(
            item => item.RentalPostId == rentalPostId
                && item.RequesterId == requesterId
                && (item.Status == ViewingAppointmentStatus.Pending
                    || item.Status == ViewingAppointmentStatus.Confirmed),
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
