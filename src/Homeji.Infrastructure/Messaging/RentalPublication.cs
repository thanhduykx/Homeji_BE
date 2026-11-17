namespace Homeji.Infrastructure.Messaging;

internal sealed class RentalPublication
{
    public Guid Id { get; init; }
    public Guid RentalPostId { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset AvailableAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? LeaseId { get; set; }
    public int Attempts { get; set; }
}
