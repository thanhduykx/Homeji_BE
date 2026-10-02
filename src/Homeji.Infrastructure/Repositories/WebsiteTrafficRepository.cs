using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class WebsiteTrafficRepository(ApplicationDbContext db) : IWebsiteTrafficRepository
{
    public async Task RecordAsync(WebsitePageView pageView, CancellationToken cancellationToken)
    {
        // The client event ID makes network retries safe even under concurrent delivery.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO homeji.website_page_views ("Id", "SessionId", "Page", "OccurredAt")
            VALUES ({pageView.Id}, {pageView.SessionId}, {pageView.Page}, {pageView.OccurredAt})
            ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);
    }

    public async Task<WebsiteTrafficReportDto> GetReportAsync(DateTimeOffset from, DateTimeOffset until, int days, CancellationToken cancellationToken)
    {
        var query = db.WebsitePageViews.AsNoTracking().Where(view => view.OccurredAt >= from && view.OccurredAt <= until);
        var pageViews = await query.CountAsync(cancellationToken);
        var sessions = await query.Select(view => view.SessionId).Distinct().CountAsync(cancellationToken);
        var active = await db.WebsitePageViews.Where(view => view.OccurredAt >= until.AddMinutes(-5) && view.OccurredAt <= until)
            .Select(view => view.SessionId).Distinct().CountAsync(cancellationToken);
        var started = await db.WebsitePageViews.Select(view => (DateTimeOffset?)view.OccurredAt).MinAsync(cancellationToken);
        // Explicit timezone conversion avoids provider DateTimeOffset/DateTime group-key coercion.
        var daily = await db.Database.SqlQuery<WebsiteTrafficDayDto>($"""
            SELECT ("OccurredAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS "Date",
                   count(*)::int AS "PageViews", count(DISTINCT "SessionId")::int AS "Sessions"
            FROM homeji.website_page_views
            WHERE "OccurredAt" >= {from} AND "OccurredAt" <= {until}
            GROUP BY ("OccurredAt" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date
            """).ToListAsync(cancellationToken);
        var topRows = await query.GroupBy(view => view.Page)
            .Select(group => new { Page = group.Key, PageViews = group.Count(), Sessions = group.Select(view => view.SessionId).Distinct().Count() })
            .OrderByDescending(page => page.PageViews).ThenBy(page => page.Page).Take(10).ToListAsync(cancellationToken);
        var top = topRows.Select(page => new WebsiteTrafficPageDto(page.Page, page.PageViews, page.Sessions)).ToArray();
        var localFrom = from.ToOffset(TimeSpan.FromHours(7));
        var trend = Enumerable.Range(0, days).Select(offset =>
        {
            var date = DateOnly.FromDateTime(localFrom.AddDays(offset).DateTime);
            var point = daily.FirstOrDefault(row => row.Date == date);
            return new WebsiteTrafficDayDto(date, point?.PageViews ?? 0, point?.Sessions ?? 0);
        }).ToArray();
        return new WebsiteTrafficReportDto(until, days, started, pageViews, sessions, active, trend, top);
    }
}
