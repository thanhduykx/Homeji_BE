using Homeji.Application.Common.Exceptions;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Authorization;

public sealed class RoleAuthorizationTests
{
    [Theory]
    [InlineData(UserRole.Renter)]
    [InlineData(UserRole.Admin)]
    public void EnsureLandlord_WhenRoleIsNotLandlord_ThrowsForbidden(UserRole role)
    {
        var profile = CreateProfile(role);

        Assert.Throws<ForbiddenAccessException>(() => UserContext.EnsureLandlord(profile));
    }

    [Theory]
    [InlineData(UserRole.Landlord)]
    [InlineData(UserRole.Admin)]
    public void EnsureRenter_WhenRoleIsNotRenter_ThrowsForbidden(UserRole role)
    {
        var profile = CreateProfile(role);

        Assert.Throws<ForbiddenAccessException>(() => UserContext.EnsureRenter(profile));
    }

    [Fact]
    public void EnsureLandlord_WithLandlord_DoesNotThrow()
    {
        var profile = CreateProfile(UserRole.Landlord);

        UserContext.EnsureLandlord(profile);
    }

    [Fact]
    public void EnsureRenter_WithRenter_DoesNotThrow()
    {
        var profile = CreateProfile(UserRole.Renter);

        UserContext.EnsureRenter(profile);
    }

    private static UserProfile CreateProfile(UserRole role)
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Role test", DateTimeOffset.UtcNow);
        if (role == UserRole.Admin)
        {
            profile.SetRole(role, DateTimeOffset.UtcNow);
            return profile;
        }

        profile.UpdateLifestyle(
            role,
            SleepHabit.EarlyBird,
            PetPreference.NoPets,
            SmokingPreference.NonSmoking,
            2_000_000,
            "Hoa Lac",
            DateTimeOffset.UtcNow);
        return profile;
    }
}
