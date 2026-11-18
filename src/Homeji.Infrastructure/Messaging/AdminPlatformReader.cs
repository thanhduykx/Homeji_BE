using Homeji.Application.Common;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.Services.Admin;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Homeji.Infrastructure.Messaging;

internal sealed class AdminPlatformReader(
    ApplicationDbContext db, IConfiguration configuration, TimeProvider timeProvider) : IAdminPlatformReader
{
    private const int SampleLimit = 1000;
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public async Task<AdminPlatformSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var startDate = RentalPublicAvailability.VietnamDate(now).AddDays(-6);
        var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), VietnamOffset).ToUniversalTime();
        var attempts = await db.RentalPublications.AsNoTracking()
            .Where(item => item.PublishedAt == null).OrderBy(item => item.AvailableAt).ThenBy(item => item.Id)
            .Select(item => item.Attempts).Take(SampleLimit + 1).ToListAsync(cancellationToken);
        var dailyCounts = await db.Database.SqlQuery<AdminPublicationDay>($"""
            SELECT (occurred_at AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS "Date", count(*) AS "Count"
            FROM homeji.rental_publication_facts
            WHERE occurred_at >= {start} AND occurred_at <= {now}
            GROUP BY 1
            """).ToDictionaryAsync(day => day.Date, day => day.Count, cancellationToken);
        var days = Enumerable.Range(0, 7).Select(startDate.AddDays)
            .Select(date => new AdminPublicationDay(date, dailyCounts.GetValueOrDefault(date))).ToArray();
        var jobsEnabled = configuration.GetValue("BackgroundJobs:Enabled", true);
        return new(now, jobsEnabled && configuration.IsRentalPublicationWorkerEnabled(),
            jobsEnabled && configuration.IsRentalPublicationAnalyticsEnabled(), configuration.GetValue("Telemetry:Enabled", true),
            Math.Min(attempts.Count, SampleLimit), attempts.Take(SampleLimit).Count(attempt => attempt > 0),
            attempts.Count > SampleLimit, days);
    }
}
