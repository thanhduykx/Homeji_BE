using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class AdminAnalyticsQueryTests
{
    [Fact]
    public void Snapshot_left_joins_owner_role_without_losing_listings_or_requiring_a_database_connection()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=translation_only;Username=translation_only;Password=unused").Options);
        var now = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        var sql = new AdminAnalyticsRepository(db).BuildRentalSnapshotQuery(now.AddDays(-30), now).ToQueryString();
        Assert.Contains("LEFT JOIN", sql, StringComparison.Ordinal);
        Assert.Contains(".role", sql, StringComparison.Ordinal);
        Assert.Contains(".type", sql, StringComparison.Ordinal);
        Assert.Contains("owner_id", sql, StringComparison.Ordinal);
        Assert.Contains("created_at <=", sql, StringComparison.Ordinal);
        Assert.Contains("created_at >=", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("LIMIT", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RentalPostType.RoommateShare, UserRole.Renter, "person_month")]
    [InlineData(RentalPostType.RoommateShare, UserRole.Landlord, "room_month")]
    [InlineData(RentalPostType.RoommateShare, UserRole.Admin, "unknown")]
    [InlineData(RentalPostType.RoommateShare, null, "unknown")]
    [InlineData(RentalPostType.VacantRoom, null, "room_month")]
    [InlineData(RentalPostType.RoomTransfer, UserRole.Renter, "room_month")]
    public void Repository_uses_the_existing_domain_price_unit_rule(RentalPostType type, UserRole? role, string expected)
    {
        Assert.Equal(expected, AdminAnalyticsRepository.ResolvePriceBasis(type, role));
    }
}
