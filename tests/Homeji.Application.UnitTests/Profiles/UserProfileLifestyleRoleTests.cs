using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Profiles;

public sealed class UserProfileLifestyleRoleTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(UserRole.Renter)]
    [InlineData(UserRole.Landlord)]
    public void Existing_admin_keeps_authority_when_updating_lifestyle(UserRole requestedRole)
    {
        var profile = CreateProfile(UserRole.Admin);
        var updatedAt = CreatedAt.AddHours(1);
        profile.UpdateLifestyle(requestedRole, SleepHabit.EarlyBird, PetPreference.NoPets,
            SmokingPreference.NonSmoking, 3_000_000, "Linh Trung", updatedAt);
        Assert.Equal(UserRole.Admin, profile.Role);
        Assert.Equal(SleepHabit.EarlyBird, profile.SleepHabit);
        Assert.Equal(PetPreference.NoPets, profile.PetPreference);
        Assert.Equal(SmokingPreference.NonSmoking, profile.SmokingPreference);
        Assert.Equal(3_000_000, profile.MaxBudget);
        Assert.Equal("Linh Trung", profile.PreferredArea);
        Assert.True(profile.OnboardingCompleted);
        Assert.Equal(updatedAt, profile.UpdatedAt);
    }

    [Theory]
    [InlineData(UserRole.Renter)]
    [InlineData(UserRole.Landlord)]
    [InlineData(UserRole.Admin)]
    public void Lifestyle_cannot_request_admin_even_for_an_existing_admin(UserRole existingRole)
    {
        var profile = CreateProfile(existingRole);
        Assert.Throws<DomainException>(() => profile.UpdateLifestyle(UserRole.Admin, SleepHabit.EarlyBird,
            PetPreference.NoPets, SmokingPreference.NonSmoking, 3_000_000, "Linh Trung", CreatedAt.AddHours(1)));
        Assert.Equal(existingRole, profile.Role);
        Assert.Equal(CreatedAt, profile.UpdatedAt);
        Assert.False(profile.OnboardingCompleted);
    }

    [Theory]
    [InlineData(UserRole.Renter, UserRole.Renter)]
    [InlineData(UserRole.Renter, UserRole.Landlord)]
    [InlineData(UserRole.Landlord, UserRole.Renter)]
    [InlineData(UserRole.Landlord, UserRole.Landlord)]
    public void Ordinary_roles_keep_their_existing_self_service_choice(UserRole existingRole, UserRole requestedRole)
    {
        var profile = CreateProfile(existingRole);
        profile.UpdateLifestyle(requestedRole, SleepHabit.EarlyBird, PetPreference.NoPets,
            SmokingPreference.NonSmoking, null, null, CreatedAt.AddHours(1));
        Assert.Equal(requestedRole, profile.Role);
        Assert.True(profile.OnboardingCompleted);
    }

    private static UserProfile CreateProfile(UserRole role)
    {
        var profile = UserProfile.Create(Guid.NewGuid(), "Role regression", CreatedAt);
        profile.SetRole(role, CreatedAt);
        return profile;
    }
}
