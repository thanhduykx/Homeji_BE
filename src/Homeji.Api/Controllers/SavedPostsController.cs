using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.SavedPosts;
using Homeji.Application.IServices.SavedPosts;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/saved-posts")]
public sealed class SavedPostsController : ControllerBase
{
    private readonly ISavedPostService _savedPostService;

    public SavedPostsController(ISavedPostService savedPostService)
    {
        _savedPostService = savedPostService;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RentalPostSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalPostSummaryDto>>> GetMine(CancellationToken cancellationToken)
    {
        return Ok(await _savedPostService.GetMineAsync(cancellationToken));
    }

    [HttpPut("{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Save(Guid postId, CancellationToken cancellationToken)
    {
        await _savedPostService.SaveAsync(postId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{postId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unsave(Guid postId, CancellationToken cancellationToken)
    {
        await _savedPostService.UnsaveAsync(postId, cancellationToken);
        return NoContent();
    }

    [HttpGet("{postId:guid}/roommate-candidates")]
    [ProducesResponseType<IReadOnlyList<RoommateCandidateDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RoommateCandidateDto>>> GetRoommateCandidates(Guid postId, CancellationToken cancellationToken)
    {
        return Ok(await _savedPostService.GetRoommateCandidatesAsync(postId, cancellationToken));
    }
}
