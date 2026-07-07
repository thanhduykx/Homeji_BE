using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Mvc;

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

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RentalPostSummaryDto>>(StatusCodes.Status200OK)]
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
                pageSize),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{postId:guid}")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RentalPostDto>> GetDetail(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _rentalPostService.GetDetailAsync(postId, cancellationToken));
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
}
