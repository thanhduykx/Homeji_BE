using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class ChatMessage
{
    public const int MaxContentLength = 4_000;

    private ChatMessage()
    {
        Content = null!;
    }

    private ChatMessage(
        Guid id,
        Guid conversationId,
        ChatMessageSender sender,
        string content,
        DateTimeOffset createdAt)
    {
        Id = id;
        ConversationId = conversationId;
        Sender = sender;
        Content = NormalizeContent(content);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public ChatMessageSender Sender { get; private set; }

    public string Content { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static ChatMessage CreateUserMessage(
        Guid conversationId,
        string content,
        DateTimeOffset createdAt)
    {
        return Create(conversationId, ChatMessageSender.User, content, createdAt);
    }

    public static ChatMessage CreateAssistantMessage(
        Guid conversationId,
        string content,
        DateTimeOffset createdAt)
    {
        return Create(conversationId, ChatMessageSender.Assistant, content, createdAt);
    }

    private static ChatMessage Create(
        Guid conversationId,
        ChatMessageSender sender,
        string content,
        DateTimeOffset createdAt)
    {
        if (conversationId == Guid.Empty)
        {
            throw new DomainException("Mã cuộc trò chuyện không được trống.");
        }

        if (!Enum.IsDefined(sender))
        {
            throw new DomainException("Người gửi tin nhắn không hợp lệ.");
        }

        return new ChatMessage(Guid.NewGuid(), conversationId, sender, content, createdAt);
    }

    private static string NormalizeContent(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException("Nội dung tin nhắn là bắt buộc.");
        }

        if (normalized.Length > MaxContentLength)
        {
            throw new DomainException($"Nội dung tin nhắn không được vượt quá {MaxContentLength} ký tự.");
        }

        return normalized;
    }
}
