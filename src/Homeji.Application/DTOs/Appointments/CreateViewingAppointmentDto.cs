namespace Homeji.Application.DTOs.Appointments;

public sealed record CreateViewingAppointmentDto(DateTimeOffset ScheduledAt, string? Note);
