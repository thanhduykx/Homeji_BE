using Homeji.Api.DesignTime;
using Homeji.Infrastructure.Repositories;

namespace Homeji.Api.IntegrationTests;

public sealed class WebsiteTrafficDatabaseTests
{
    [TrafficDatabaseFact]
    public async Task Report_executes_postgres_aggregation_and_returns_consistent_daily_totals()
    {
        await using var db = new ApplicationDbContextFactory().CreateDbContext([]);
        var now = DateTimeOffset.UtcNow;
        var from = new DateTimeOffset(now.ToOffset(TimeSpan.FromHours(7)).Date.AddDays(-29), TimeSpan.FromHours(7)).ToUniversalTime();
        var report = await new WebsiteTrafficRepository(db).GetReportAsync(from, now, 30, CancellationToken.None);
        Assert.Equal(30, report.Trend.Count);
        Assert.Equal(report.PageViews, report.Trend.Sum(day => day.PageViews));
        Assert.InRange(report.TopPages.Count, 0, 10);
        Assert.All(report.TopPages, page => Assert.InRange(page.Sessions, 0, page.PageViews));
    }
}

// Explicit opt-in: this test reads the configured PostgreSQL database, never writes it.
public sealed class TrafficDatabaseFactAttribute : FactAttribute
{
    public TrafficDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HOMEJI_TEST_TRAFFIC_DATABASE") != "1")
            Skip = "Set HOMEJI_TEST_TRAFFIC_DATABASE=1 to verify against the configured PostgreSQL database.";
    }
}
