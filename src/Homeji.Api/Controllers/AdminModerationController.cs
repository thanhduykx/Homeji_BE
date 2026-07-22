using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.Reports;
using Homeji.Application.IServices.Admin;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/admin/moderation")]
public sealed class AdminModerationController : ControllerBase
{
    private readonly IAdminModerationService _moderationService;

    public AdminModerationController(IAdminModerationService moderationService)
    {
        _moderationService = moderationService;
    }

    [HttpGet("rental-posts/pending")]
    [ProducesResponseType<IReadOnlyList<RentalPostSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalPostSummaryDto>>> GetPendingRentalPosts(CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.GetPendingRentalPostsAsync(cancellationToken));
    }

    [HttpPost("rental-posts/{postId:guid}/approve")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostDto>> ApproveRentalPost(
        Guid postId,
        [FromBody] ApproveRentalPostDto? request,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.ApproveRentalPostAsync(
            postId,
            request ?? new ApproveRentalPostDto(null),
            cancellationToken));
    }

    [HttpPost("rental-posts/{postId:guid}/reject")]
    [ProducesResponseType<RentalPostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalPostDto>> RejectRentalPost(
        Guid postId,
        [FromBody] RejectRentalPostDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.RejectRentalPostAsync(postId, request, cancellationToken));
    }

    [HttpGet("reports")]
    [ProducesResponseType<IReadOnlyList<ReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportDto>>> GetReports(
        [FromQuery] ReportStatus? status,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.GetReportsAsync(status, cancellationToken));
    }

    [HttpPost("reports/{reportId:guid}/resolve")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDto>> ResolveReport(
        Guid reportId,
        [FromBody] ResolveReportDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.ResolveReportAsync(reportId, request, cancellationToken));
    }

    [HttpPost("reports/{reportId:guid}/reject")]
    [ProducesResponseType<ReportDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ReportDto>> RejectReport(
        Guid reportId,
        [FromBody] ResolveReportDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _moderationService.RejectReportAsync(reportId, request, cancellationToken));
    }
}
