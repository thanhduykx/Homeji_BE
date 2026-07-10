namespace Homeji.Application.DTOs.AI;

public sealed record AiHighlightResponseDto(
    AiParsedSearchCriteriaDto Criteria,
    IReadOnlyCollection<AiHighlightedRentalPostDto> Posts,
    string Tag,
    string? MapFocusAddress,
    decimal? MapFocusLatitude,
    decimal? MapFocusLongitude);
