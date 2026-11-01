namespace Homeji.Domain.Entities;

public sealed class AgentPostDraft
{
    private AgentPostDraft() { Payload = null!; }
    public AgentPostDraft(Guid userId, string payload, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); UserId = userId; Payload = payload; CreatedAt = now; ExpiresAt = now.AddHours(1);
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Payload { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public bool Cancelled { get; private set; }
    public Guid? PublishedPostId { get; private set; }
    public string? ConfirmedDetails { get; private set; }
    public void Cancel() { if (!PublishedPostId.HasValue) { Cancelled = true; Payload = "{}"; } }
    public void Publish(Guid postId, string confirmedDetails)
    {
        PublishedPostId = postId; ConfirmedDetails = confirmedDetails;
        // Original image bytes are needed only while awaiting approval.
        Payload = "{}";
    }
}
