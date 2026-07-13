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
    string? ContactAddress,
    string? RentalNeed,
    SleepHabit SleepHabit,
    PetPreference PetPreference,
    SmokingPreference SmokingPreference,
    decimal? MaxBudget,
    bool OnboardingCompleted,
    LandlordVerificationStatus LandlordVerificationStatus,
    bool IsPremium,
    string SubscriptionBadge,
    DateTimeOffset? PremiumExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
