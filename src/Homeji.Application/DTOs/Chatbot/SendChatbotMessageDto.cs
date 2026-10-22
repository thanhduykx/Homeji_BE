using Homeji.Application.DTOs.AI;

namespace Homeji.Application.DTOs.Chatbot;

public sealed record SendChatbotMessageDto(
    Guid? ConversationId,
    string? Message,
    bool SaveHistory = false,
    AiParsedSearchCriteriaDto? PreviousCriteria = null,
    ChatbotRentalContextDto? RentalContext = null);

public sealed record ChatbotRentalContextDto(Guid PostId, string? Task = null);
