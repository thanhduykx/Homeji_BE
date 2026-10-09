namespace Homeji.Application.DTOs.AI;

public sealed record AiHighlightResponseDto(
    AiParsedSearchCriteriaDto Criteria,
    IReadOnlyCollection<AiHighlightedRentalPostDto> Posts,
    string Tag,
    string? MapFocusAddress,
    decimal? MapFocusLatitude,
    decimal? MapFocusLongitude)
{
    public IReadOnlyCollection<AiHighlightedRentalPostDto> NeedsConfirmation { get; init; } = [];
    public IReadOnlyCollection<string> Clarifications { get; init; } = [];
    public bool CompareRequested { get; init; }
}
