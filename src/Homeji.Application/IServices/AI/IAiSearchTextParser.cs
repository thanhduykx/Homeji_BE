using Homeji.Application.DTOs.AI;

namespace Homeji.Application.IServices.AI;

public interface IAiSearchTextParser
{
    Task<AiParsedSearchCriteriaDto> ParseAsync(
        string text,
        CancellationToken cancellationToken = default);
}
