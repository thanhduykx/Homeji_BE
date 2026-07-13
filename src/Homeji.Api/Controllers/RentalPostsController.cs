using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/rental-posts")]
public sealed class RentalPostsController : ControllerBase
{
    private readonly IRentalPostService _rentalPostService;

    public RentalPostsController(IRentalPostService rentalPostService)
    {
        _rentalPostService = rentalPostService;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicSearch)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RentalPostSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<IReadOnlyList<RentalPostSummaryDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? minArea,
        [FromQuery] decimal? maxArea,
        [FromQuery] decimal? minLatitude,
        [FromQuery] decimal? maxLatitude,
        [FromQuery] decimal? minLongitude,
        [FromQuery] decimal? maxLongitude,
        [FromQuery] string[]? amenities,
        [FromQuery] decimal? maxDeposit = null,
        [FromQuery] int? minAvailableSlots = null,
        [FromQuery] DateOnly? availableFromBefore = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _rentalPostService.SearchAsync(
            new RentalPostSearchDto(
                keyword,
                minPrice,
                maxPrice,
                minArea,
                maxArea,
                minLatitude,
                maxLatitude,
                minLongitude,
                maxLongitude,
                amenities ?? [],
                page,
                pageSize,
                maxDeposit,
                minAvailableSlots,
                availableFromBefore),
            cancellationToken);

        return Ok(result);
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [HttpGet("{postId:guid}")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RentalPostDto>> GetDetail(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.GetDetailAsync(postId, cancellationToken));
    }

    [HttpPost("compare")]
    [ProducesResponseType<RentalPostComparisonDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostComparisonDto>> Compare(
        [FromBody] CompareRentalPostsDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.CompareAsync(request, cancellationToken));
    }

    [HttpPost("drafts")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RentalPostDto>> CreateDraft(
        [FromBody] CreateRentalPostDraftDto request,
        CancellationToken cancellationToken)
    {
        var post = await _rentalPostService.CreateDraftAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { postId = post.Id }, post);
    }

    [HttpGet("mine/stats")]
    [ProducesResponseType<RentalPostOwnerStatsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostOwnerStatsDto>> GetOwnerStats(CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.GetOwnerStatsAsync(cancellationToken));
    }

    [HttpPut("{postId:guid}")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostDto>> Update(
        Guid postId,
        [FromBody] UpdateRentalPostDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.UpdateAsync(postId, request, cancellationToken));
    }

    [HttpPost("{postId:guid}/media")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostDto>> AddMedia(
        Guid postId,
        [FromBody] AddRentalPostMediaDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.AddMediaAsync(postId, request, cancellationToken));
    }

    [HttpDelete("{postId:guid}/media/{mediaId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMedia(Guid postId, Guid mediaId, CancellationToken cancellationToken)
    {
        await _rentalPostService.DeleteMediaAsync(postId, mediaId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{postId:guid}/submit")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostDto>> Submit(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.SubmitAsync(postId, cancellationToken));
    }

    [HttpPost("{postId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid postId, CancellationToken cancellationToken)
    {
        await _rentalPostService.ArchiveAsync(postId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{postId:guid}/mark-rented")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRented(Guid postId, CancellationToken cancellationToken)
    {
        await _rentalPostService.MarkRentedAsync(postId, cancellationToken);
        return NoContent();
    }
}
