namespace Homeji.Domain.Entities;

/// <summary>Bounded per-user personalization aggregate. No cross-user behavioral profiling.</summary>
public sealed class SearchPersonalization
{
    public Guid UserId { get; set; }
    public bool Enabled { get; set; }
    public bool AiEnabled { get; set; }
    public bool EmailEnabled { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public string Data { get; set; } = "{}";
    public long Version { get; set; }
}
