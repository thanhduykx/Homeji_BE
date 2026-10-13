using Homeji.Application.DTOs.RentalPosts;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class RentalTransferExpiryQueryTests
{
    [Fact]
    public void Expired_transfers_are_filtered_before_search_candidate_pagination()
    {
        // SQL translation only: no connection is opened and no database is mutated.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_only;Username=translation_only;Password=unused").Options);
        var search = new RentalPostSearchDto(null, null, null, null, null, null, null, null, null,
            [], Page: 2, PageSize: 100);
        var sql = new RentalPostRepository(db, new Clock()).BuildActiveSearchQuery(search).ToQueryString();
        var where = sql.IndexOf("WHERE", StringComparison.Ordinal);
        var limit = sql.IndexOf("LIMIT", StringComparison.Ordinal);
        Assert.True(where >= 0 && limit > where);
        var beforePagination = sql[where..limit];
        Assert.Contains("original_lease_ends_on", beforePagination, StringComparison.Ordinal);
        Assert.Contains(new DateOnly(2026, 10, 13).ToString(System.Globalization.CultureInfo.InvariantCulture), sql, StringComparison.Ordinal);
        Assert.Matches(@"original_lease_ends_on >= @", beforePagination);
        Assert.Contains("original_lease_ends_on IS NULL", beforePagination, StringComparison.Ordinal);
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 12, 17, 0, 0, TimeSpan.Zero);
    }
}
