using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RentalWantedPost
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2_000;
    public const int MaxPreferredAreaLength = 300;
    public const int MaxAmenityLength = 60;

    private RentalWantedPost()
    {
        Title = null!;
        Description = null!;
        PreferredArea = null!;
        AmenityCodes = [];
    }

    public RentalWantedPost(
        Guid requesterId,
        string title,
        string description,
        string preferredArea,
        decimal maxBudget,
        int occupantCount,
        IReadOnlyCollection<string> amenityCodes,
        DateOnly desiredMoveInDate,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        RequesterId = requesterId;
        Status = WantedPostStatus.Active;
        SetDetails(title, description, preferredArea, maxBudget, occupantCount, amenityCodes, desiredMoveInDate);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid RequesterId { get; private set; }
    public WantedPostStatus Status { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string PreferredArea { get; private set; } = null!;
    public decimal MaxBudget { get; private set; }
    public int OccupantCount { get; private set; }
    public string[] AmenityCodes { get; private set; } = [];
    public DateOnly DesiredMoveInDate { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string title,
        string description,
        string preferredArea,
        decimal maxBudget,
        int occupantCount,
        IReadOnlyCollection<string> amenityCodes,
        DateOnly desiredMoveInDate,
        DateTimeOffset updatedAt)
    {
        EnsureActive();
        SetDetails(title, description, preferredArea, maxBudget, occupantCount, amenityCodes, desiredMoveInDate);
        UpdatedAt = updatedAt;
    }

    public void Close(DateTimeOffset updatedAt)
    {
        Status = WantedPostStatus.Closed;
        UpdatedAt = updatedAt;
    }

    private void SetDetails(
        string title,
        string description,
        string preferredArea,
        decimal maxBudget,
        int occupantCount,
        IReadOnlyCollection<string> amenityCodes,
        DateOnly desiredMoveInDate)
    {
        Title = Normalize(title, MaxTitleLength, nameof(Title));
        Description = Normalize(description, MaxDescriptionLength, nameof(Description));
        PreferredArea = Normalize(preferredArea, MaxPreferredAreaLength, nameof(PreferredArea));
        MaxBudget = maxBudget > 0 ? maxBudget : throw new DomainException("Ngân sách tối đa phải lớn hơn 0.");
        OccupantCount = occupantCount > 0 ? occupantCount : throw new DomainException("Số người ở phải lớn hơn 0.");
        AmenityCodes = amenityCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => Normalize(code, MaxAmenityLength, "Amenity").ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        DesiredMoveInDate = desiredMoveInDate;
    }

    private void EnsureActive()
    {
        if (Status != WantedPostStatus.Active)
        {
            throw new DomainException("Chỉ tin tìm phòng đang hoạt động mới được sửa.");
        }
    }

    private static string Normalize(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} là bắt buộc và không quá {maxLength} ký tự.");
        }

        return normalized;
    }
}
