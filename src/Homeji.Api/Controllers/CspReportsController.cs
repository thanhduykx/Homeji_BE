using System.Text.Json;
using Homeji.Api.Middlewares;
using Homeji.Api.RateLimiting;
using Homeji.Api.Security;
using Homeji.Application.Abstractions.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
public sealed class CspReportsController(ICspReportLog log) : ControllerBase
{
    private const int MaxBodyBytes = 16 * 1024;

    [AllowAnonymous]
    [SkipUserActivity]
    [EnableRateLimiting(RateLimitingPolicyNames.CspReports)]
    [Consumes("application/csp-report")]
    [HttpPost("api/csp-reports")]
    public async Task<IActionResult> Report(CancellationToken cancellationToken)
    {
        var body = new byte[MaxBodyBytes + 1];
        var length = await Request.Body.ReadAtLeastAsync(body, body.Length, throwOnEndOfStream: false, cancellationToken);
        if (length > MaxBodyBytes) return StatusCode(StatusCodes.Status413PayloadTooLarge);

        try
        {
            using var payload = JsonDocument.Parse(body.AsMemory(0, length));
            var violation = CspReportParser.Parse(payload.RootElement);
            if (violation is not null) log.Record(violation);
            return NoContent();
        }
        catch (JsonException)
        {
            return BadRequest();
        }
    }
}
