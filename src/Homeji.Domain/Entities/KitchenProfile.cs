using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class KitchenProfile
{
    public const int MaxKitchenNameLength = 100;
    public const int MaxKitchenBioLength = 500;
    public const int MaxHygieneCertificateUrlLength = 500;

    private KitchenProfile() { }

    public KitchenProfile(Guid userId, string kitchenName, string? kitchenBio = null)
    {
        if (userId == Guid.Empty) throw new DomainException("Mã người dùng không hợp lệ.");
        UserId = userId;
        Update(kitchenName, true, 0, 5.0m, kitchenBio, null, DateTimeOffset.UtcNow);
    }

    public Guid UserId { get; private set; }
    public string KitchenName { get; private set; } = null!;
    public bool IsOpenNow { get; private set; } = true;
    public int OperatingShifts { get; private set; }
    public decimal MaxDeliveryDistanceKm { get; private set; } = 5.0m;
    public string? KitchenBio { get; private set; }
    public string? HygieneCertificateUrl { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(
        string kitchenName,
        bool isOpenNow,
        int operatingShifts,
        decimal maxDeliveryDistanceKm,
        string? kitchenBio,
        string? hygieneCertificateUrl,
        DateTimeOffset now)
    {
        var name = kitchenName?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tên bếp không được để trống.");
        if (name.Length > MaxKitchenNameLength)
            throw new DomainException($"Tên bếp không được vượt quá {MaxKitchenNameLength} ký tự.");

        if (maxDeliveryDistanceKm is < 0 or > 50)
            throw new DomainException("Bán kính giao hàng tối đa phải từ 0 đến 50 km.");

        var bio = kitchenBio?.Trim();
        if (bio?.Length > MaxKitchenBioLength)
            throw new DomainException($"Mô tả bếp không được vượt quá {MaxKitchenBioLength} ký tự.");

        var cert = hygieneCertificateUrl?.Trim();
        if (cert?.Length > MaxHygieneCertificateUrlLength)
            throw new DomainException($"Chứng nhận ATTP không được vượt quá {MaxHygieneCertificateUrlLength} ký tự.");

        KitchenName = name;
        IsOpenNow = isOpenNow;
        OperatingShifts = operatingShifts;
        MaxDeliveryDistanceKm = maxDeliveryDistanceKm;
        KitchenBio = string.IsNullOrEmpty(bio) ? null : bio;
        HygieneCertificateUrl = string.IsNullOrEmpty(cert) ? null : cert;
        UpdatedAt = now;
    }

    public void ToggleOpenStatus(bool isOpen, DateTimeOffset now)
    {
        IsOpenNow = isOpen;
        UpdatedAt = now;
    }
}
