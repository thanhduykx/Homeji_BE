namespace Homeji.Application.DTOs.Chatbot;

public enum ChatbotNavigationActionKind
{
    OpenSection = 1,
    Navigate = 2,
}

public sealed record ChatbotNavigationActionDto(
    string Id,
    string Label,
    string Description,
    ChatbotNavigationActionKind Kind,
    string Target);
