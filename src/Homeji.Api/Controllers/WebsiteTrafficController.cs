using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IServices.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
public sealed class WebsiteTrafficController(IWebsiteTrafficService traffic) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.WebsiteTraffic)]
    [RequestSizeLimit(1024)]
    [HttpPost("api/analytics/page-views")]
    public async Task<IActionResult> Record(RecordWebsitePageViewDto request, CancellationToken cancellationToken)
    {
        await traffic.RecordAsync(request, cancellationToken);
        return NoContent();
    }

    [Authorize]
    [HttpGet("api/admin/analytics/traffic")]
    public async Task<ActionResult<WebsiteTrafficReportDto>> Report([FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        Ok(await traffic.GetReportAsync(days, cancellationToken));
}
