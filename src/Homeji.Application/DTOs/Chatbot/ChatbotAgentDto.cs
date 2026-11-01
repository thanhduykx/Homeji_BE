namespace Homeji.Application.DTOs.Chatbot;

public sealed record AgentPostDetails(string Title, string Description, decimal Price, string Condition,
    string Category, string Address, decimal Latitude, decimal Longitude)
{
    public string Kind { get; init; } = "secondHand";
    public decimal? Deposit { get; init; }
    public decimal? Area { get; init; }
    public int? MaxOccupants { get; init; }
    public int? AvailableSlots { get; init; }
    public int? MinimumLeaseMonths { get; init; }
    public string? HouseRules { get; init; }
    public string[] Amenities { get; init; } = [];
    public DateOnly? AvailableFrom { get; init; }
    public int? TransferKind { get; init; }
    public DateOnly? OriginalLeaseEndsOn { get; init; }
    public decimal? PassFee { get; init; }
    public string? TransferReason { get; init; }
    public bool OwnerConsentConfirmed { get; init; }
    public string? OwnerConsentContact { get; init; }
    public int? AvailableQuantity { get; init; }
    public string? Unit { get; init; }
    public int? PreparationMinutes { get; init; }
}
public sealed record AgentPostDraftDto(Guid Id, AgentPostDetails Details, IReadOnlyList<string> Images,
    DateTimeOffset ExpiresAt, Guid? PublishedPostId);
public sealed record AgentPostResultDto(AgentPostDraftDto? Draft, bool UpgradeRequired, string Message);
