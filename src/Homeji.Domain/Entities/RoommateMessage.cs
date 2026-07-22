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
            throw new DomainException("Mã cuộc trò chuyện và người gửi không được trống.");
        }

        var normalizedBody = body?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedBody))
        {
            throw new DomainException("Nội dung tin nhắn là bắt buộc.");
        }

        if (normalizedBody.Length > MaxBodyLength)
        {
            throw new DomainException($"Nội dung tin nhắn không được vượt quá {MaxBodyLength} ký tự.");
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
