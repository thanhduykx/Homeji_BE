using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class ChatConversation
{
    public const int MaxTitleLength = 160;

    private readonly List<ChatMessage> _messages = [];

    private ChatConversation()
    {
        Title = null!;
    }

    private ChatConversation(Guid id, Guid userId, string title, DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        Title = NormalizeTitle(title);
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string Title { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<ChatMessage> Messages => _messages;

    public static ChatConversation Create(Guid userId, string title, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Mã người dùng không được để trống.");
        }

        return new ChatConversation(Guid.NewGuid(), userId, title, createdAt);
    }

    public ChatMessage AddUserMessage(string content, DateTimeOffset createdAt)
    {
        var message = ChatMessage.CreateUserMessage(Id, content, createdAt);
        _messages.Add(message);
        UpdatedAt = createdAt;
        return message;
    }

    public ChatMessage AddAssistantMessage(string content, DateTimeOffset createdAt)
    {
        var message = ChatMessage.CreateAssistantMessage(Id, content, createdAt);
        _messages.Add(message);
        UpdatedAt = createdAt;
        return message;
    }

    public void Rename(string title, DateTimeOffset updatedAt)
    {
        Title = NormalizeTitle(title);
        UpdatedAt = updatedAt;
    }

    private static string NormalizeTitle(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "Homeji chat";
        }

        return normalized.Length <= MaxTitleLength ? normalized : normalized[..MaxTitleLength];
    }
}
