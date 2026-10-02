namespace Homeji.Application.DTOs.Admin;

public sealed record RecordWebsitePageViewDto(Guid EventId, Guid SessionId, string Page);
public sealed record WebsiteTrafficDayDto(DateOnly Date, int PageViews, int Sessions);
public sealed record WebsiteTrafficPageDto(string Page, int PageViews, int Sessions);
public sealed record WebsiteTrafficReportDto(
    DateTimeOffset GeneratedAt, int PeriodDays, DateTimeOffset? TrackingStartedAt,
    int PageViews, int Sessions, int ActiveSessions,
    IReadOnlyList<WebsiteTrafficDayDto> Trend, IReadOnlyList<WebsiteTrafficPageDto> TopPages);
