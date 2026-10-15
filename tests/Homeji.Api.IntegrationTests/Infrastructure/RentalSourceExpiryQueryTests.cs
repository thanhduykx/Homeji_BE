using Homeji.Application.DTOs.RentalPosts;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class RentalSourceExpiryQueryTests
{
    [Fact]
    public void Expired_sources_are_filtered_using_the_injected_clock_before_pagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_only;Username=translation_only;Password=unused").Options);
        var clock = new FixedClock(new DateTimeOffset(2026, 10, 15, 4, 0, 0, TimeSpan.Zero));
        var sql = new RentalSourceListingRepository(db, clock)
            .BuildSearchQuery(new RentalSourceSearchDto(null, "quan-9", null, null, Page: 2)).ToQueryString();
        Assert.Contains("source_expires_at IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("source_expires_at >", sql, StringComparison.Ordinal);
        Assert.Contains("2026-10-15T04:00:00", sql, StringComparison.Ordinal);
        Assert.True(sql.IndexOf("source_expires_at >", StringComparison.Ordinal) < sql.IndexOf("LIMIT", StringComparison.Ordinal));
        Assert.Contains("OFFSET", sql, StringComparison.Ordinal);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
