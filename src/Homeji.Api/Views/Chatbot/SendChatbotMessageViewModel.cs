namespace Homeji.Api.Views.Chatbot;

public sealed record SendChatbotMessageViewModel(
    Guid? ConversationId,
    string? Message);
