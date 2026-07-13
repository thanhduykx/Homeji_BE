using Homeji.Application.DTOs.Appointments;

namespace Homeji.Application.IServices.Appointments;

public interface IViewingAppointmentService
{
    Task<ViewingAppointmentDto> CreateAsync(Guid rentalPostId, CreateViewingAppointmentDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ViewingAppointmentDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task<ViewingAppointmentDto> ConfirmAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ViewingAppointmentDto> RejectAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ViewingAppointmentDto> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}
