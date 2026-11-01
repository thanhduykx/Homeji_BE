using Homeji.Application.DTOs.Admin;
using Homeji.Application.IServices.Admin;
using Homeji.Api.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/reporting")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminReportingController(IAdminReportingService reporting) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<ActionResult<AdminPageDto<AdminRegisteredAccountDto>>> Accounts(string? keyword, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await reporting.GetAccountsAsync(keyword, page, pageSize, cancellationToken));

    [HttpGet("members")]
    public async Task<ActionResult<AdminPageDto<AdminMemberDto>>> Members(string? keyword, string? state, int page = 1, int pageSize = 25, CancellationToken cancellationToken = default) =>
        Ok(await reporting.GetMembersAsync(keyword, state, page, pageSize, cancellationToken));

    [HttpGet("exports")]
    public async Task<ActionResult<IReadOnlyList<AdminReportExportDto>>> Exports(CancellationToken cancellationToken) => Ok(await reporting.GetExportsAsync(cancellationToken));

    [HttpPost("exports")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<ActionResult<AdminReportExportDto>> Create(CreateAdminReportDto request, CancellationToken cancellationToken)
    {
        var export = await reporting.CreateAsync(request, cancellationToken);
        return Created($"/api/admin/reporting/exports/{export.Id}/download", export);
    }

    [HttpGet("exports/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var file = await reporting.DownloadAsync(id, cancellationToken);
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(file.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.FileName);
    }
}
