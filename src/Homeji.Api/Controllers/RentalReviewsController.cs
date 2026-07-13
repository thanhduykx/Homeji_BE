using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.Reviews;
using Homeji.Application.IServices.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/rental-posts/{rentalPostId:guid}/reviews")]
public sealed class RentalReviewsController : ControllerBase
{
    private readonly IRentalReviewService _reviewService;

    public RentalReviewsController(IRentalReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [HttpGet]
    [ProducesResponseType<RentalReviewCollectionDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalReviewCollectionDto>> GetByPost(
        Guid rentalPostId,
        CancellationToken cancellationToken)
    {
        return Ok(await _reviewService.GetByPostAsync(rentalPostId, cancellationToken));
    }

    [HttpPut("mine")]
    [ProducesResponseType<RentalReviewDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalReviewDto>> UpsertMine(
        Guid rentalPostId,
        [FromBody] UpsertRentalReviewDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _reviewService.UpsertAsync(rentalPostId, request, cancellationToken));
    }

    [HttpDelete("mine")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMine(Guid rentalPostId, CancellationToken cancellationToken)
    {
        await _reviewService.DeleteMineAsync(rentalPostId, cancellationToken);
        return NoContent();
    }
}
