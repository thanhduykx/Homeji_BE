using Homeji.Domain.Exceptions;
using Homeji.Domain.Enums;

namespace Homeji.Domain.Entities;

public sealed class UserProfile
{
    public const int MaxDisplayNameLength = 100;
    public const int MaxPhoneLength = 30;
    public const int MaxSchoolLength = 150;
    public const int MaxAreaLength = 150;
    public const int MaxAvatarPathLength = 500;

    private UserProfile()
    {
        DisplayName = null!;
        Role = UserRole.Renter;
        LandlordVerificationStatus = LandlordVerificationStatus.None;
    }

    private UserProfile(Guid id, string displayName, DateTimeOffset createdAt)
    {
        Id = id;
        DisplayName = NormalizeDisplayName(displayName);
        Role = UserRole.Renter;
        LandlordVerificationStatus = LandlordVerificationStatus.None;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

    public UserRole Role { get; private set; }

    public string? Phone { get; private set; }

    public string? AvatarPath { get; private set; }

    public string? School { get; private set; }

    public string? PreferredArea { get; private set; }

    public SleepHabit SleepHabit { get; private set; }

    public PetPreference PetPreference { get; private set; }

    public SmokingPreference SmokingPreference { get; private set; }

    public decimal? MaxBudget { get; private set; }

    public bool OnboardingCompleted { get; private set; }

    public LandlordVerificationStatus LandlordVerificationStatus { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile Create(Guid userId, string displayName, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id must not be empty.");
        }

        return new UserProfile(userId, displayName, createdAt);
    }

    public void UpdateDisplayName(string displayName, DateTimeOffset updatedAt)
    {
        DisplayName = NormalizeDisplayName(displayName);
        UpdatedAt = updatedAt;
    }

    public void UpdateProfile(
        string displayName,
        string? phone,
        string? avatarPath,
        string? school,
        string? preferredArea,
        DateTimeOffset updatedAt)
    {
        DisplayName = NormalizeDisplayName(displayName);
        Phone = NormalizeOptional(phone, MaxPhoneLength, nameof(Phone));
        AvatarPath = NormalizeOptional(avatarPath, MaxAvatarPathLength, nameof(AvatarPath));
        School = NormalizeOptional(school, MaxSchoolLength, nameof(School));
        PreferredArea = NormalizeOptional(preferredArea, MaxAreaLength, nameof(PreferredArea));
        UpdatedAt = updatedAt;
    }

    public void UpdateLifestyle(
        UserRole role,
        SleepHabit sleepHabit,
        PetPreference petPreference,
        SmokingPreference smokingPreference,
        decimal? maxBudget,
        string? preferredArea,
        DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(role) || role == UserRole.Admin)
        {
            throw new DomainException("User role must be renter or landlord.");
        }

        if (maxBudget is <= 0)
        {
            throw new DomainException("Max budget must be greater than zero.");
        }

        Role = role;
        SleepHabit = sleepHabit;
        PetPreference = petPreference;
        SmokingPreference = smokingPreference;
        MaxBudget = maxBudget;
        PreferredArea = NormalizeOptional(preferredArea, MaxAreaLength, nameof(PreferredArea));
        OnboardingCompleted = true;
        UpdatedAt = updatedAt;
    }

    public void SetRole(UserRole role, DateTimeOffset updatedAt)
    {
        Role = role;
        UpdatedAt = updatedAt;
    }

    public void SubmitLandlordVerification(DateTimeOffset updatedAt)
    {
        if (Role != UserRole.Landlord)
        {
            throw new DomainException("Only landlord profiles can submit verification.");
        }

        if (LandlordVerificationStatus == LandlordVerificationStatus.Pending)
        {
            throw new DomainException("A landlord verification request is already pending.");
        }

        if (LandlordVerificationStatus == LandlordVerificationStatus.Verified)
        {
            throw new DomainException("This landlord profile is already verified.");
        }

        LandlordVerificationStatus = LandlordVerificationStatus.Pending;
        UpdatedAt = updatedAt;
    }

    public void CompleteLandlordVerification(bool approved, DateTimeOffset updatedAt)
    {
        if (LandlordVerificationStatus != LandlordVerificationStatus.Pending)
        {
            throw new DomainException("Only pending landlord verification can be reviewed.");
        }

        LandlordVerificationStatus = approved
            ? LandlordVerificationStatus.Verified
            : LandlordVerificationStatus.Rejected;
        UpdatedAt = updatedAt;
    }

    private static string NormalizeDisplayName(string displayName)
    {
        var normalized = displayName?.Trim();

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException("Display name is required.");
        }

        if (normalized.Length > MaxDisplayNameLength)
        {
            throw new DomainException($"Display name must not exceed {MaxDisplayNameLength} characters.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }
}
