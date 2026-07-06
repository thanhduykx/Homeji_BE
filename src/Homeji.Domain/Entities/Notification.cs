using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class Notification
{
    public const int MaxTitleLength = 200;
    public const int MaxMessageLength = 1_000;

    private Notification()
    {
        Title = null!;
        Message = null!;
    }

    public Notification(
        Guid recipientId,
        NotificationType type,
        string title,
        string message,
        Guid? relatedEntityId,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        RecipientId = recipientId;
        Type = type;
        Title = Normalize(title, MaxTitleLength, nameof(Title));
        Message = Normalize(message, MaxMessageLength, nameof(Message));
        RelatedEntityId = relatedEntityId;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid RecipientId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; }

    public string Message { get; private set; }

    public Guid? RelatedEntityId { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public void MarkRead(DateTimeOffset readAt)
    {
        IsRead = true;
        ReadAt ??= readAt;
    }

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
}
