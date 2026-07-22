using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class PostMessage
{
    public const int MaxBodyLength = 2_000;

    private PostMessage()
    {
        Body = null!;
    }

    public PostMessage(Guid conversationId, Guid senderId, string body, DateTimeOffset sentAt)
    {
        var normalized = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > MaxBodyLength)
        {
            throw new DomainException($"Nội dung tin nhắn là bắt buộc và không quá {MaxBodyLength} ký tự.");
        }

        Id = Guid.NewGuid();
        ConversationId = conversationId;
        SenderId = senderId;
        Body = normalized;
        SentAt = sentAt;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string Body { get; private set; }
    public DateTimeOffset SentAt { get; private set; }
}
