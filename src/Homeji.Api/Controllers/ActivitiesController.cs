using Homeji.Application.DTOs.Activities;
using Homeji.Application.IServices.Activities;
using Microsoft.AspNetCore.Mvc;
using Homeji.Domain.Enums;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/activities")]
public sealed class ActivitiesController : ControllerBase
{
    private readonly IUserActivityService _activities;

    public ActivitiesController(IUserActivityService activities)
    {
        _activities = activities;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserActivityDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserActivityDto>>> GetMine(
        [FromQuery] UserActivityType? type = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _activities.GetMineAsync(type, take, cancellationToken));
    }

    [HttpPost("presence")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult RecordPresence() => NoContent();
}
