using Homeji.Api.DesignTime;
using Homeji.Infrastructure.Repositories;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests;

public sealed class WebsiteTrafficDatabaseTests
{
    [TrafficDatabaseFact]
    public async Task Report_executes_postgres_aggregation_and_returns_consistent_daily_totals()
    {
        var localConnection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE");
        await using var db = string.IsNullOrEmpty(localConnection)
            ? new ApplicationDbContextFactory().CreateDbContext([])
            : new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(localConnection).Options);
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
[AttributeUsage(AttributeTargets.Method)]
public sealed class TrafficDatabaseFactAttribute : FactAttribute
{
    public TrafficDatabaseFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("HOMEJI_TEST_TRAFFIC_DATABASE") != "1"
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")))
            Skip = "Set HOMEJI_TEST_TRAFFIC_DATABASE=1 to verify against the configured PostgreSQL database.";
    }
}
