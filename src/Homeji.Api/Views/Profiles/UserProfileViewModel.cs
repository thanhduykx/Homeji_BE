namespace Homeji.Api.Views.Profiles;

public sealed record UserProfileViewModel(
    Guid Id,
    string DisplayName,
    Homeji.Domain.Enums.UserRole Role,
    string? Phone,
    string? AvatarPath,
    string? School,
    string? PreferredArea,
    Homeji.Domain.Enums.SleepHabit SleepHabit,
    Homeji.Domain.Enums.PetPreference PetPreference,
    Homeji.Domain.Enums.SmokingPreference SmokingPreference,
    decimal? MaxBudget,
    bool OnboardingCompleted,
    Homeji.Domain.Enums.LandlordVerificationStatus LandlordVerificationStatus,
    bool IsPremium,
    string SubscriptionBadge,
    DateTimeOffset? PremiumExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
