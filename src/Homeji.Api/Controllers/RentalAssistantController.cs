using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/rental-assistant")]
[EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
public sealed class RentalAssistantController(IRentalDecisionService service) : ControllerBase
{
    [HttpPost("compare")]
    public async Task<ActionResult<RentalDecisionResponseDto>> Compare(RentalDecisionRequestDto request, CancellationToken cancellationToken)
        => Ok(await service.CompareAsync(request, cancellationToken));

    [HttpPost("draft-preview")]
    public async Task<ActionResult<RentalDraftPreviewDto>> PreviewDraft(RentalDraftPreviewRequestDto request, CancellationToken cancellationToken)
        => Ok(await service.PreviewDraftAsync(request, cancellationToken));

    [HttpGet("admin-summary")]
    public async Task<ActionResult<AdminAssistantSummaryDto>> AdminSummary(CancellationToken cancellationToken)
        => Ok(await service.GetAdminSummaryAsync(cancellationToken));
}
