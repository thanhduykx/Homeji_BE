using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Route("api/receipts")]
public sealed class ReceiptsController(ITransactionReceiptService receipts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ReceiptPageDto>> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) => Ok(await receipts.GetMineAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TransactionReceiptDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await receipts.GetMineByIdAsync(id, cancellationToken));

    [HttpGet("{id:guid}/html")]
    public async Task<IActionResult> GetHtml(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'; frame-ancestors 'self'";
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return Content(await receipts.GetMyHtmlAsync(id, cancellationToken), "text/html; charset=utf-8");
    }

    [HttpGet("{id:guid}/html-content")]
    public async Task<IActionResult> GetHtmlContent(Guid id, CancellationToken cancellationToken) =>
        new JsonResult(await receipts.GetMyHtmlAsync(id, cancellationToken));
}
