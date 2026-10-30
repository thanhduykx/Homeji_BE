using Homeji.Application.DTOs.Moderation;
using Homeji.Application.IServices.Moderation;
using Microsoft.AspNetCore.Mvc;
namespace Homeji.Api.Controllers;
[ApiController]
[Route("api/admin/ai-reviews")]
public sealed class AiListingReviewsController(IAiListingModeration moderation) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<AiReviewDashboardDto>> Get(CancellationToken ct) => Ok(await moderation.GetDashboardAsync(ct));
    [HttpPut("settings")] public async Task<IActionResult> Settings(AiReviewSettingsDto settings, CancellationToken ct) { await moderation.UpdateSettingsAsync(settings, ct); return NoContent(); }
    [HttpPost("backfill")] public async Task<IActionResult> Backfill(CancellationToken ct) { await moderation.QueueBackfillAsync(ct); return NoContent(); }
    [HttpPost("{id:guid}/resolve")] public async Task<IActionResult> Resolve(Guid id, AiReviewDecisionDto decision, CancellationToken ct) { await moderation.ResolveAsync(id, decision.Restore, ct); return NoContent(); }
}
