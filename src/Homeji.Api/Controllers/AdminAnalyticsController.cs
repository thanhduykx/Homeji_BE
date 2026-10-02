using Homeji.Application.DTOs.Admin;
using Homeji.Application.IServices.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/analytics")]
public sealed class AdminAnalyticsController : ControllerBase
{
    private readonly IAdminAnalyticsService _analytics;

    public AdminAnalyticsController(IAdminAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("product")]
    [ProducesResponseType<AdminProductAnalyticsDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AdminProductAnalyticsDto>> GetProductAnalytics(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _analytics.GetProductAnalyticsAsync(days, cancellationToken));
    }
}
