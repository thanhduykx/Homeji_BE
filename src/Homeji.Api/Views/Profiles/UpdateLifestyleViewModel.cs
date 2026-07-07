using Homeji.Domain.Enums;

namespace Homeji.Api.Views.Profiles;

public sealed record UpdateLifestyleViewModel(
    UserRole Role,
    SleepHabit SleepHabit,
    PetPreference PetPreference,
    SmokingPreference SmokingPreference,
    decimal? MaxBudget,
    string? PreferredArea);
