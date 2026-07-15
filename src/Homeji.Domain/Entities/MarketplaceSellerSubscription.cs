using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class MarketplaceSellerSubscription
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 100;

    private MarketplaceSellerSubscription()
    {
        PackageCode = null!;
        PackageName = null!;
    }

    public MarketplaceSellerSubscription(
        Guid userId,
        string packageCode,
        string packageName,
        decimal price,
        decimal commissionRate,
        int durationDays,
        DateTimeOffset startsAt,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty || durationDays <= 0)
        {
            throw new DomainException("Seller subscription user and duration are invalid.");
        }

        if (price <= 0 || commissionRate is <= 0 or >= 1)
        {
            throw new DomainException("Seller subscription price or commission rate is invalid.");
        }

        Id = Guid.NewGuid();
        UserId = userId;
        PackageCode = Normalize(packageCode, MaxCodeLength, nameof(PackageCode));
        PackageName = Normalize(packageName, MaxNameLength, nameof(PackageName));
        Price = price;
        CommissionRate = commissionRate;
        StartsAt = startsAt;
        ExpiresAt = startsAt.AddDays(durationDays);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string PackageCode { get; private set; }
    public string PackageName { get; private set; }
    public decimal Price { get; private set; }
    public decimal CommissionRate { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsActiveAt(DateTimeOffset now) => StartsAt <= now && ExpiresAt > now;

    private static string Normalize(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} is required and must not exceed {maxLength} characters.");
        }

        return normalized;
    }
}
