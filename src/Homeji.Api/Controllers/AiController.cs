using Homeji.Api.Mappers;
using Homeji.Api.Views.AI;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Homeji.Api.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/ai")]
[EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
public sealed class AiController : ControllerBase
{
    private readonly IAiSearchService _aiSearch;
    private readonly IRentalDraftService _drafts;

    public AiController(IAiSearchService aiSearch, IRentalDraftService drafts)
    {
        _aiSearch = aiSearch;
        _drafts = drafts;
    }

    [HttpPost("rental-draft")]
    [ProducesResponseType<RentalDraftResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<RentalDraftResponseDto>> GenerateDraft(
        [FromBody] RentalDraftRequestDto request, CancellationToken cancellationToken) =>
        Ok(await _drafts.GenerateAsync(request, cancellationToken));

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
