using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Profiles;

public sealed record UpdateLifestyleDto(
    UserRole Role,
    SleepHabit SleepHabit,
    PetPreference PetPreference,
    SmokingPreference SmokingPreference,
    decimal? MaxBudget,
    string? PreferredArea);
