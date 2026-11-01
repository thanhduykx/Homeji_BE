namespace Homeji.Application.DTOs.Chatbot;

public sealed record AgentPostDetails(string Title, string Description, decimal Price, string Condition,
    string Category, string Address, decimal Latitude, decimal Longitude);
public sealed record AgentPostDraftDto(Guid Id, AgentPostDetails Details, IReadOnlyList<string> Images,
    DateTimeOffset ExpiresAt, Guid? PublishedPostId);
public sealed record AgentPostResultDto(AgentPostDraftDto? Draft, bool UpgradeRequired, string Message);
