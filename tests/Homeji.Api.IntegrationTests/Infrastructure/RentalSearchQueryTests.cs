using Homeji.Application.DTOs.RentalPosts;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class RentalSearchQueryTests
{
    [Fact]
    public void Roommate_type_is_filtered_in_postgres_before_pagination()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_only;Username=translation_only;Password=unused").Options);
        var request = new RentalPostSearchDto(null, null, null, null, null, null, null, null, null,
            [], Page: 2, PageSize: 20, MinAvailableSlots: 1, Type: Homeji.Domain.Enums.RentalPostType.RoommateShare);
        var sql = new RentalPostRepository(db).BuildActiveSearchQuery(request).ToQueryString();
        Assert.Matches(@"\w+\.type = @", sql);
        Assert.Contains("available_slots >=", sql, StringComparison.Ordinal);
        Assert.True(sql.IndexOf(".type =", StringComparison.Ordinal) < sql.IndexOf("LIMIT", StringComparison.Ordinal));
        Assert.Contains("OFFSET", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Shortlist_filters_translate_to_parameterized_postgres_before_paging_without_a_connection()
    {
        // Translation only: this test never opens a connection or writes to a database.
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_only;Username=translation_only;Password=unused").Options);
        var ids = new[] { Guid.NewGuid() };
        var request = new RentalPostSearchDto(null, null, 4_000_000, null, null, 10.7m, 10.93m, 106.72m, 106.9m,
            ["KITCHEN"], 1, 5, MinAvailableSlots: 2, ExcludedAmenities: ["AIR_CONDITIONER"], ExcludeRoommateShare: true, Ids: ids, ExcludeSynthetic: true);
        var sql = new RentalPostRepository(db).BuildActiveSearchQuery(request).ToQueryString();
        Assert.Contains("ANY (", sql, StringComparison.Ordinal);
        Assert.Contains("NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("EXISTS", sql, StringComparison.Ordinal);
        Assert.Matches(@"NOT \(\w+\.is_synthetic\)", sql);
        Assert.Contains("available_slots >=", sql, StringComparison.Ordinal);
        Assert.Contains("latitude >=", sql, StringComparison.Ordinal);
        Assert.Contains("longitude >=", sql, StringComparison.Ordinal);
        Assert.True(sql.IndexOf("WHERE", StringComparison.Ordinal) < sql.IndexOf("LIMIT", StringComparison.Ordinal));
        Assert.Contains("@", sql, StringComparison.Ordinal);
    }
}
