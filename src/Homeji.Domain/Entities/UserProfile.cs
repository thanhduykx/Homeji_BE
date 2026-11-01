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
    public const int MaxContactAddressLength = 500;
    public const int MaxRentalNeedLength = 1_000;

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

    public string? FacebookUrl { get; private set; }
    public string? ZaloPhone { get; private set; }

    public string? AvatarPath { get; private set; }

    public string? School { get; private set; }

    public string? PreferredArea { get; private set; }
    public string? ContactAddress { get; private set; }
    public string? RentalNeed { get; private set; }

    public SleepHabit SleepHabit { get; private set; }

    public PetPreference PetPreference { get; private set; }

    public SmokingPreference SmokingPreference { get; private set; }

    public decimal? MaxBudget { get; private set; }

    public bool OnboardingCompleted { get; private set; }

    public LandlordVerificationStatus LandlordVerificationStatus { get; private set; }

    public DateTimeOffset? SessionsRevokedBefore { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserProfile Create(Guid userId, string displayName, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Mã người dùng không được để trống.");
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
        string? contactAddress,
        string? rentalNeed,
        DateTimeOffset updatedAt)
    {
        DisplayName = NormalizeDisplayName(displayName);
        Phone = NormalizeOptional(phone, MaxPhoneLength, nameof(Phone));
        AvatarPath = NormalizeOptional(avatarPath, MaxAvatarPathLength, nameof(AvatarPath));
        School = NormalizeOptional(school, MaxSchoolLength, nameof(School));
        PreferredArea = NormalizeOptional(preferredArea, MaxAreaLength, nameof(PreferredArea));
        ContactAddress = NormalizeOptional(contactAddress, MaxContactAddressLength, nameof(ContactAddress));
        RentalNeed = NormalizeOptional(rentalNeed, MaxRentalNeedLength, nameof(RentalNeed));
        UpdatedAt = updatedAt;
    }

    public void UpdateSocialContacts(string? facebookUrl, string? zaloPhone, DateTimeOffset updatedAt)
    {
        var facebook = facebookUrl?.Trim();
        if (string.IsNullOrEmpty(facebook)) facebook = null;
        if (facebook is not null)
        {
            if (facebook.Length > 500 || !Uri.TryCreate(facebook, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.Host is not ("facebook.com" or "www.facebook.com" or "m.facebook.com")
                || uri.AbsolutePath is "/" or "/l.php" || uri.AbsolutePath.StartsWith("/flx/", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("Nhập link Facebook hợp lệ, bắt đầu bằng https://www.facebook.com/.");
            facebook = uri.AbsoluteUri;
        }
        if (zaloPhone?.Length > 30) throw new DomainException("Số Zalo không được dài quá 30 ký tự.");
        var zalo = string.IsNullOrWhiteSpace(zaloPhone) ? null : System.Text.RegularExpressions.Regex.Replace(zaloPhone, @"[\s().-]", "");
        if (zalo?.StartsWith("+84", StringComparison.Ordinal) == true) zalo = "0" + zalo[3..];
        if (zalo is not null && !System.Text.RegularExpressions.Regex.IsMatch(zalo, @"^0[35789][0-9]{8}$"))
            throw new DomainException("Nhập số Zalo Việt Nam gồm 10 số, bắt đầu bằng 03, 05, 07, 08 hoặc 09.");
        FacebookUrl = facebook;
        ZaloPhone = zalo;
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
            throw new DomainException("Vai trò phải là người thuê hoặc chủ trọ.");
        }

        if (maxBudget is <= 0)
        {
            throw new DomainException("Ngân sách tối đa phải lớn hơn 0.");
        }

        // Lifestyle preferences cannot remove authority granted through administrator provisioning.
        if (Role != UserRole.Admin) Role = role;
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

    public void RevokeSessions(DateTimeOffset revokedBefore)
    {
        if (!SessionsRevokedBefore.HasValue || revokedBefore > SessionsRevokedBefore.Value)
        {
            SessionsRevokedBefore = revokedBefore;
            UpdatedAt = revokedBefore;
        }
    }

    public void SubmitLandlordVerification(DateTimeOffset updatedAt)
    {
        if (Role != UserRole.Landlord)
        {
            throw new DomainException("Chỉ hồ sơ chủ trọ mới gửi được yêu cầu xác minh.");
        }

        if (LandlordVerificationStatus == LandlordVerificationStatus.Pending)
        {
            throw new DomainException("Đã có yêu cầu xác minh chủ trọ đang chờ xử lý.");
        }

        if (LandlordVerificationStatus == LandlordVerificationStatus.Verified)
        {
            throw new DomainException("Hồ sơ chủ trọ này đã được xác minh.");
        }

        LandlordVerificationStatus = LandlordVerificationStatus.Pending;
        UpdatedAt = updatedAt;
    }

    public void CompleteLandlordVerification(bool approved, DateTimeOffset updatedAt)
    {
        if (LandlordVerificationStatus != LandlordVerificationStatus.Pending)
        {
            throw new DomainException("Chỉ yêu cầu xác minh đang chờ mới có thể duyệt.");
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
            throw new DomainException("Tên hiển thị là bắt buộc.");
        }

        if (normalized.Length > MaxDisplayNameLength)
        {
            throw new DomainException($"Tên hiển thị không được vượt quá {MaxDisplayNameLength} ký tự.");
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
            throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }
}
