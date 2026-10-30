using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class AiListingReview
{
    private AiListingReview() { }
    public AiListingReview(Guid subjectId, string kind, string title, string contentHash, string snapshot, DateTimeOffset sourceUpdatedAt, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); SubjectId = subjectId; Kind = kind; Title = title;
        ContentHash = contentHash; Snapshot = snapshot; SourceUpdatedAt = sourceUpdatedAt; CreatedAt = now; NextAttemptAt = now;
    }
    public Guid Id { get; private set; }
    public Guid SubjectId { get; private set; }
    public string Kind { get; private set; } = null!;
    public string Title { get; private set; } = null!;
    public string ContentHash { get; private set; } = null!;
    public string Snapshot { get; private set; } = null!;
    public DateTimeOffset SourceUpdatedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string State { get; private set; } = "pending";
    public Guid? LeaseId { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public int Attempts { get; private set; }
    public string? Category { get; private set; }
    public string? Reason { get; private set; }
    public string? Evidence { get; private set; }
    public double? Confidence { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public int? PreviousStatus { get; private set; }
    public DateTimeOffset? BlockedAt { get; private set; }
    public void TouchSource(DateTimeOffset version) => SourceUpdatedAt = version;
    public Guid Claim(DateTimeOffset now)
    {
        if (State != "pending" && !(State == "processing" && LeaseUntil <= now)) throw new DomainException("Tin này đang được kiểm tra.");
        State = "processing"; LeaseId = Guid.NewGuid(); LeaseUntil = now.AddMinutes(3); Attempts++; return LeaseId.Value;
    }
    public void Complete(string state, string category, string reason, string evidence, double confidence, DateTimeOffset now)
    {
        State = state; Category = category; Reason = reason; Evidence = evidence; Confidence = confidence; CompletedAt = now; LeaseId = null; LeaseUntil = null;
    }
    public void RecordBlock(int previousStatus, DateTimeOffset now) { PreviousStatus = previousStatus; BlockedAt = now; }
    public void Retry(DateTimeOffset next, string reason)
    {
        NextAttemptAt = next; Reason = reason; LeaseId = null; LeaseUntil = null;
        State = Attempts >= 3 ? "needs_review" : "pending";
    }
    public void Resolve(Guid admin, bool restore, DateTimeOffset now)
    {
        if (ReviewedAt.HasValue || State is not ("needs_review" or "blocked")) throw new DomainException("Kết quả này không còn chờ xử lý.");
        ReviewedBy = admin; ReviewedAt = now; State = restore ? "restored" : "confirmed";
    }
}

public sealed class AiModerationSchedule
{
    public string Id { get; private set; } = "listings-v1";
    public bool Enabled { get; set; } = true;
    public bool AutoBlock { get; set; } = true;
    public int BatchSize { get; set; } = 3;
    public int SpacingSeconds { get; set; } = 60;
    public int PauseMinutes { get; set; } = 10;
    public int DailyRequestLimit { get; set; } = 30;
    public int DailyTokenBudget { get; set; } = 200000;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? BackfillBefore { get; set; }
    public DateTimeOffset NextAllowedAt { get; set; }
    public DateTimeOffset BudgetDay { get; set; }
    public int RequestsToday { get; set; }
    public int TokensReservedToday { get; set; }
    public int InBatch { get; set; }
    public string? PauseReason { get; set; }
    public void ResetDay(DateTimeOffset now)
    {
        if (BudgetDay.Date == now.UtcDateTime.Date) return;
        BudgetDay = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero); RequestsToday = 0; TokensReservedToday = 0;
    }
    public bool Reserve(int tokens, DateTimeOffset now)
    {
        ResetDay(now);
        if (!Enabled || now < NextAllowedAt) return false;
        if (RequestsToday >= DailyRequestLimit || TokensReservedToday + tokens > DailyTokenBudget)
        {
            NextAllowedAt = BudgetDay.AddDays(1); PauseReason = "Đã đạt giới hạn kiểm tra trong ngày."; return false;
        }
        RequestsToday++; TokensReservedToday += tokens; InBatch++;
        NextAllowedAt = now.AddSeconds(SpacingSeconds);
        if (InBatch >= BatchSize) { InBatch = 0; NextAllowedAt = now.AddMinutes(PauseMinutes); }
        PauseReason = null; return true;
    }
    public void Backoff(DateTimeOffset until, string reason)
    { if (until > NextAllowedAt) NextAllowedAt = until; PauseReason = reason; InBatch = 0; }
}
