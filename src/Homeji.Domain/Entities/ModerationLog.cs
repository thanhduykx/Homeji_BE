namespace Homeji.Domain.Entities;

public sealed class ModerationLog
{
    private ModerationLog()
    {
        Action = null!;
    }

    public ModerationLog(Guid rentalPostId, Guid actorId, string action, string? reason, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        RentalPostId = rentalPostId;
        ActorId = actorId;
        Action = action;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RentalPostId { get; private set; }

    public Guid ActorId { get; private set; }

    public string Action { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
