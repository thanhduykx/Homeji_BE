using Homeji.Application.DTOs.WantedPosts;
using Homeji.Application.IServices.WantedPosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/rental-wanted-posts")]
public sealed class RentalWantedPostsController : ControllerBase
{
    private readonly IRentalWantedPostService _posts;

    public RentalWantedPostsController(IRentalWantedPostService posts)
    {
        _posts = posts;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RentalWantedPostDto>>> Search(
        [FromQuery] string? area,
        [FromQuery] decimal? maxBudget,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _posts.SearchAsync(area, maxBudget, page, pageSize, cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RentalWantedPostDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _posts.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<RentalWantedPostDto>> Create(
        [FromBody] UpsertRentalWantedPostDto request,
        CancellationToken cancellationToken)
    {
        var result = await _posts.CreateAsync(request, cancellationToken);
        return Created($"/api/rental-wanted-posts/{result.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RentalWantedPostDto>> Update(
        Guid id,
        [FromBody] UpsertRentalWantedPostDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _posts.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken)
    {
        await _posts.CloseAsync(id, cancellationToken);
        return NoContent();
    }
}
