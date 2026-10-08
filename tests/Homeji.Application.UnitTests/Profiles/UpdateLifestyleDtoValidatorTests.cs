using Homeji.Application.DTOs.Profiles;
using Homeji.Application.Services.Profiles.Validation;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Profiles;

public sealed class UpdateLifestyleDtoValidatorTests
{
    private readonly UpdateLifestyleDtoValidator _validator = new();
    private static UpdateLifestyleDto Valid => new(UserRole.Renter, SleepHabit.Unknown,
        PetPreference.Unknown, SmokingPreference.Unknown, null, null);

    [Theory]
    [InlineData("SleepHabit")]
    [InlineData("PetPreference")]
    [InlineData("SmokingPreference")]
    public void UndefinedLifestyleValue_IsRejected(string field)
    {
        var request = field switch
        {
            "SleepHabit" => Valid with { SleepHabit = (SleepHabit)999 },
            "PetPreference" => Valid with { PetPreference = (PetPreference)999 },
            _ => Valid with { SmokingPreference = (SmokingPreference)999 }
        };
        var result = _validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void UnknownPreferencesAndEmptyBudget_AreAllowed()
    {
        Assert.True(_validator.Validate(Valid).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveBudget_IsRejected(decimal budget)
    {
        Assert.False(_validator.Validate(Valid with { MaxBudget = budget }).IsValid);
    }

    [Fact]
    public void PositiveBudgetAndSpecificArea_AreAllowed()
    {
        Assert.True(_validator.Validate(Valid with { MaxBudget = 3500000, PreferredArea = "Linh Trung" }).IsValid);
    }
}
