using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class UserSubscription
{
    public const int MaxPackageCodeLength = 80;
    public const int MaxPackageNameLength = 120;

    private UserSubscription()
    {
        PackageCode = null!;
        PackageName = null!;
    }

    private UserSubscription(
        Guid id,
        Guid userId,
        SubscriptionTier tier,
        SubscriptionStatus status,
        string packageCode,
        string packageName,
        Guid? paymentTransactionId,
        DateTimeOffset startedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Tier = tier;
        Status = status;
        PackageCode = packageCode;
        PackageName = packageName;
        PaymentTransactionId = paymentTransactionId;
        StartedAt = startedAt;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public SubscriptionTier Tier { get; private set; }

    public SubscriptionStatus Status { get; private set; }

    public string PackageCode { get; private set; }

    public string PackageName { get; private set; }

    public Guid? PaymentTransactionId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserSubscription CreatePremium(
        Guid userId,
        string packageCode,
        string packageName,
        Guid? paymentTransactionId,
        DateTimeOffset startedAt,
        int durationDays,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("User id must not be empty.");
        }

        if (durationDays <= 0)
        {
            throw new DomainException("Premium duration must be greater than zero.");
        }

        return new UserSubscription(
            Guid.NewGuid(),
            userId,
            SubscriptionTier.Premium,
            SubscriptionStatus.Active,
            NormalizeRequired(packageCode, MaxPackageCodeLength, nameof(PackageCode)),
            NormalizeRequired(packageName, MaxPackageNameLength, nameof(PackageName)),
            paymentTransactionId,
            startedAt,
            startedAt.AddDays(durationDays),
            createdAt);
    }

    public bool IsActivePremium(DateTimeOffset now)
    {
        return Tier == SubscriptionTier.Premium
            && Status == SubscriptionStatus.Active
            && StartedAt <= now
            && ExpiresAt > now;
    }

    public void Expire(DateTimeOffset expiredAt)
    {
        if (Status == SubscriptionStatus.Expired)
        {
            return;
        }

        Status = SubscriptionStatus.Expired;
        UpdatedAt = expiredAt;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{fieldName} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }
}
