using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class UserProfile
{
    public const int MaxDisplayNameLength = 100;

    private UserProfile()
    {
        DisplayName = null!;
    }

    private UserProfile(Guid id, string displayName, DateTimeOffset createdAt)
    {
        Id = id;
        DisplayName = NormalizeDisplayName(displayName);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

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
}
