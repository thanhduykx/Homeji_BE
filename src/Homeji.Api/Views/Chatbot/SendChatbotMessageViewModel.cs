using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Api.Views.Chatbot;

public sealed record SendChatbotMessageViewModel(
    Guid? ConversationId,
    string? Message,
    bool SaveHistory = false,
    AiParsedSearchCriteriaDto? PreviousCriteria = null,
    ChatbotRentalContextDto? RentalContext = null,
    ChatbotRentalSelectionDto? RentalSelection = null,
    ChatbotFoodContextDto? FoodContext = null,
    ChatbotFoodCriteriaDto? PreviousFoodCriteria = null);
