using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class AdminWorkQueueQueryTests
{
    [Fact]
    public void Tasks_translate_to_bounded_projections_without_media_or_target_enrichment()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_only;Username=translation_only;Password=unused").Options);
        var repository = new AdminWorkQueueRepository(db);
        Assert.Contains("status = 2", repository.BuildPostTasksQuery().ToQueryString(), StringComparison.Ordinal);
        Assert.Contains("status = 1", repository.BuildReportTasksQuery().ToQueryString(), StringComparison.Ordinal);
        foreach (var sql in new[] { repository.BuildPostTasksQuery().ToQueryString(), repository.BuildReportTasksQuery().ToQueryString() })
        {
            Assert.Contains("LIMIT", sql, StringComparison.Ordinal);
            Assert.Contains("10", sql, StringComparison.Ordinal);
            Assert.Contains("ORDER BY", sql, StringComparison.Ordinal);
            Assert.Contains("created_at", sql, StringComparison.Ordinal);
            Assert.Contains("WHERE", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("JOIN", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("description", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("media", sql, StringComparison.Ordinal);
            Assert.DoesNotContain("target_id", sql, StringComparison.Ordinal);
        }
    }
}
