using Homeji.Application.DTOs.AI;

namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotReplyDto(
    Guid ConversationId,
    ChatbotMessageDto UserMessage,
    ChatbotMessageDto AssistantMessage,
    AiHighlightResponseDto? SearchUpdate,
    IReadOnlyCollection<ChatbotNavigationActionDto> Actions)
{
    public IReadOnlyList<Homeji.Application.DTOs.Roommates.RoommateDirectoryCandidateDto>? RoommateCandidates { get; init; }
    public AgentPostDraftDto? AgentDraft { get; init; }
    public ChatbotNearbyDto? NearbyRooms { get; init; }
    public bool AgentRequested { get; init; }
    public bool UpgradeRequired { get; init; }
    public RentalDecisionResponseDto? RentalDecision { get; init; }
    public ChatbotFoodResponseDto? FoodDiscovery { get; init; }
    public ChatbotSourceMatchDto? SourceRooms { get; init; }
}
