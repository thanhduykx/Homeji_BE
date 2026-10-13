using Homeji.Application.Common;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.RentalPosts;

public sealed class RentalPublicAvailabilityTests
{
    [Theory]
    [InlineData(RentalPostType.RoomTransfer, -1, true)]
    [InlineData(RentalPostType.RoomTransfer, 0, false)]
    [InlineData(RentalPostType.RoomTransfer, 1, false)]
    [InlineData(RentalPostType.RoomTransfer, null, false)]
    [InlineData(RentalPostType.VacantRoom, -1, false)]
    [InlineData(RentalPostType.RoommateShare, -1, false)]
    public void Transfer_is_valid_through_end_of_Vietnam_lease_day_and_unknown_is_not_expired(RentalPostType type, int? offset, bool expired)
    {
        var today = new DateOnly(2026, 10, 13);
        Assert.Equal(expired, RentalPublicAvailability.IsExpiredTransfer(type, offset.HasValue ? today.AddDays(offset.Value) : null, today));
    }

    [Fact]
    public void Expiry_rolls_at_Vietnam_midnight_while_UTC_day_has_not_changed()
    {
        var before = new DateTimeOffset(2026, 10, 12, 16, 59, 59, TimeSpan.Zero);
        var after = before.AddSeconds(1);
        var leaseEnd = new DateOnly(2026, 10, 12);
        Assert.Equal(new DateOnly(2026, 10, 12), RentalPublicAvailability.Today(new Clock(before)));
        Assert.Equal(new DateOnly(2026, 10, 13), RentalPublicAvailability.Today(new Clock(after)));
        Assert.False(RentalPublicAvailability.IsExpiredTransfer(RentalPostType.RoomTransfer, leaseEnd, RentalPublicAvailability.VietnamDate(before)));
        Assert.True(RentalPublicAvailability.IsExpiredTransfer(RentalPostType.RoomTransfer, leaseEnd, RentalPublicAvailability.VietnamDate(after)));
        Assert.Equal(before.UtcDateTime.Date, after.UtcDateTime.Date);
    }

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
