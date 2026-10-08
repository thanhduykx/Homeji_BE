using Homeji.Application.DTOs.AI;

namespace Homeji.Api.Views.Chatbot;

public sealed record SendChatbotMessageViewModel(
    Guid? ConversationId,
    string? Message,
    bool SaveHistory = false,
    AiParsedSearchCriteriaDto? PreviousCriteria = null);
