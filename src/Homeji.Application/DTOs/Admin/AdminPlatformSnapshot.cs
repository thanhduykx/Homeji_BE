namespace Homeji.Application.DTOs.Admin;

public sealed record AdminPublicationDay(DateOnly Date, long Count);

public sealed record AdminPlatformSnapshot(
    DateTimeOffset CheckedAt,
    bool PublisherConfigured,
    bool AnalyticsConfigured,
    bool TelemetryConfigured,
    int PendingSample,
    int RetryingInSample,
    bool SampleLimited,
    IReadOnlyList<AdminPublicationDay> PublicationDays);
