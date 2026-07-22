namespace Homeji.Application.DTOs.Admin;

public sealed record MaintenanceAnnouncementRequestDto(
    string? Title,
    string? Message,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ScheduledEndAt);

public sealed record MaintenanceAnnouncementResultDto(
    string Title,
    string Message,
    DateTimeOffset? ScheduledStartAt,
    DateTimeOffset? ScheduledEndAt,
    int RecipientCount,
    int OnlineRecipientCount,
    DateTimeOffset SentAt);
