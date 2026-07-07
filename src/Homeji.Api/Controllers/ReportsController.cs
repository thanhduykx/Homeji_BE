using Homeji.Application.DTOs.Reports;
using Homeji.Application.IServices.Reports;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpPost]
    [ProducesResponseType<ReportDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ReportDto>> Create([FromBody] CreateReportDto request, CancellationToken cancellationToken)
    {
        var report = await _reportService.CreateAsync(request, cancellationToken);
        return Created($"/api/reports/{report.Id}", report);
    }
}
