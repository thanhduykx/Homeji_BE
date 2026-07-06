using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Profiles;

public sealed record UserProfileDto(
    Guid Id,
    string DisplayName,
    UserRole Role,
    string? Phone,
    string? AvatarPath,
    string? School,
    string? PreferredArea,
    SleepHabit SleepHabit,
    PetPreference PetPreference,
    SmokingPreference SmokingPreference,
    decimal? MaxBudget,
    bool OnboardingCompleted,
    LandlordVerificationStatus LandlordVerificationStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
