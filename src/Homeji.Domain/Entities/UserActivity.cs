using Homeji.Domain.Exceptions;
using Homeji.Domain.Enums;

namespace Homeji.Domain.Entities;

public sealed class UserActivity
{
    public const int MaxActionLength = 200;
    public const int MaxPathLength = 500;
    public const int MaxMethodLength = 10;
    public const int MaxDetailsLength = 1_000;

    private UserActivity()
    {
        Action = null!;
        ResourcePath = null!;
        HttpMethod = null!;
    }

    public UserActivity(
        Guid userId,
        string action,
        string resourcePath,
        string httpMethod,
        int responseStatusCode,
        DateTimeOffset occurredAt,
        UserActivityType type = UserActivityType.General,
        Guid? relatedEntityId = null,
        string? details = null)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Action = Normalize(action, MaxActionLength, nameof(Action));
        ResourcePath = Normalize(resourcePath, MaxPathLength, nameof(ResourcePath));
        HttpMethod = Normalize(httpMethod, MaxMethodLength, nameof(HttpMethod)).ToUpperInvariant();
        ResponseStatusCode = responseStatusCode;
        Type = type;
        RelatedEntityId = relatedEntityId;
        Details = NormalizeOptional(details, MaxDetailsLength, nameof(Details));
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Action { get; private set; }
    public string ResourcePath { get; private set; }
    public string HttpMethod { get; private set; }
    public int ResponseStatusCode { get; private set; }
    public UserActivityType Type { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    private static string Normalize(string value, int maxLength, string fieldName)
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

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{fieldName} must not exceed {maxLength} characters.");
    }
}
