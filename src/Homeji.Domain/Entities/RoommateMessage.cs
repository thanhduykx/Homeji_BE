using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class RoommateMessage
{
    public const int MaxBodyLength = 2_000;

    private RoommateMessage()
    {
        Body = null!;
    }

    public RoommateMessage(Guid conversationId, Guid senderId, string body, DateTimeOffset sentAt)
    {
        if (conversationId == Guid.Empty || senderId == Guid.Empty)
        {
            throw new DomainException("Conversation id and sender id must not be empty.");
        }

        var normalizedBody = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            throw new DomainException("Message body is required.");
        }

        if (normalizedBody.Length > MaxBodyLength)
        {
            throw new DomainException($"Message body must not exceed {MaxBodyLength} characters.");
        }

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        SenderId = senderId;
        Body = normalizedBody;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid SenderId { get; private set; }

    public string Body { get; private set; }

    public DateTimeOffset SentAt { get; private set; }
}
