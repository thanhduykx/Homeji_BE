namespace Homeji.Application.DTOs.Moderation;
public sealed record AiReviewVerdict(string Decision, string Category, string Reason, string Evidence, double Confidence);
public sealed record AiReviewSettingsDto(bool Enabled, bool AutoBlock, int BatchSize, int SpacingSeconds, int PauseMinutes, int DailyRequestLimit, int DailyTokenBudget);
public sealed record AiReviewItemDto(Guid Id, Guid SubjectId, string Kind, string Title, string State, string? Category, string? Reason, string? Evidence, double? Confidence, int Attempts, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ReviewedAt, string Description);
public sealed record AiReviewDashboardDto(AiReviewSettingsDto Settings, DateTimeOffset NextAllowedAt, int RequestsToday, int TokensReservedToday, string? PauseReason, int Pending, int NeedsReview, int Blocked, IReadOnlyList<AiReviewItemDto> Items);
public sealed record AiReviewDecisionDto(bool Restore);
