using Homeji.Application.DTOs.AI;

namespace Homeji.Application.DTOs.Chatbot;

public sealed record SendChatbotMessageDto(
    Guid? ConversationId,
    string? Message,
    bool SaveHistory = false,
    AiParsedSearchCriteriaDto? PreviousCriteria = null,
    ChatbotRentalContextDto? RentalContext = null,
    ChatbotRentalSelectionDto? RentalSelection = null,
    ChatbotFoodContextDto? FoodContext = null,
    ChatbotFoodCriteriaDto? PreviousFoodCriteria = null);

public sealed record ChatbotRentalContextDto(Guid PostId, string? Task = null);
public sealed record ChatbotRentalSelectionDto(IReadOnlyCollection<Guid>? PostIds,
    string? Task = null, RentalCostScenarioDto? Scenario = null);
