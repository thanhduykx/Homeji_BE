using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Appointments;

public sealed record ViewingAppointmentDto(
    Guid Id,
    Guid RentalPostId,
    string RentalPostTitle,
    Guid RequesterId,
    Guid OwnerId,
    DateTimeOffset ScheduledAt,
    string? Note,
    ViewingAppointmentStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
