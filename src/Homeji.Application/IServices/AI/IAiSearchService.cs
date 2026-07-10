using Homeji.Application.DTOs.AI;

namespace Homeji.Application.IServices.AI;

public interface IAiSearchService
{
    Task<AiParsedSearchCriteriaDto> ParseSearchAsync(
        AiParseSearchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<AiHighlightResponseDto> HighlightRentalPostsAsync(
        AiHighlightRequestDto request,
        CancellationToken cancellationToken = default);
}
