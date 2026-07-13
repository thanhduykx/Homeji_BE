using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Appointments;

public interface IViewingAppointmentRepository
{
    Task AddAsync(ViewingAppointment appointment, CancellationToken cancellationToken = default);
    Task<ViewingAppointment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ViewingAppointment>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> HasActiveAsync(Guid rentalPostId, Guid requesterId, CancellationToken cancellationToken = default);
    Task<bool> HasCompletedAsync(Guid rentalPostId, Guid requesterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> CountByPostIdsAsync(IReadOnlyCollection<Guid> rentalPostIds, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
