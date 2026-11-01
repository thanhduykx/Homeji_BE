namespace Homeji.Domain.Entities;

public sealed class MaintenanceAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
