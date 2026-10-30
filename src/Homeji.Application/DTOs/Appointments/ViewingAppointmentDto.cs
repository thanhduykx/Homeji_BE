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
    DateTimeOffset UpdatedAt,
    int? ReminderMinutesBefore = null,
    DateTimeOffset? ReminderDueAt = null,
    DateTimeOffset? ReminderSentAt = null,
    bool ReminderFailed = false);
