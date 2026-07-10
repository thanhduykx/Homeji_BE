using Homeji.Api.Mappers;
using Homeji.Api.Views.AI;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/ai")]
public sealed class AiController : ControllerBase
{
    private readonly IAiSearchService _aiSearch;

    public AiController(IAiSearchService aiSearch)
    {
        _aiSearch = aiSearch;
    }

    [HttpPost("parse-search")]
    [ProducesResponseType<AiParsedSearchCriteriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AiParsedSearchCriteriaDto>> ParseSearch(
        [FromBody] AiParseSearchViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _aiSearch.ParseSearchAsync(AiViewMapper.ToDto(request), cancellationToken));
    }

    [HttpPost("highlight-rental-posts")]
    [ProducesResponseType<AiHighlightResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AiHighlightResponseDto>> HighlightRentalPosts(
        [FromBody] AiHighlightRentalPostsViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _aiSearch.HighlightRentalPostsAsync(AiViewMapper.ToDto(request), cancellationToken));
    }
}
