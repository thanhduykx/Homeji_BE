namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotPopupConfigDto(
    bool Enabled,
    string Title,
    string Greeting,
    IReadOnlyCollection<string> SuggestedPrompts);
