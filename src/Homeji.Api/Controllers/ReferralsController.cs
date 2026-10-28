using System.ComponentModel.DataAnnotations;
using Homeji.Application.IServices.Promotions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Homeji.Api.Controllers;
[ApiController, Authorize, Route("api/referrals")]
public sealed class ReferralsController(IReferralService referrals) : ControllerBase
{
    public sealed record ClaimRequest([Required, RegularExpression("^[A-Fa-f0-9]{24}$")] string Code);
    public sealed record QuoteRequest(Guid VoucherId, [Required, RegularExpression("^(food|vip)$")] string Purpose, [Range(1, 1000000000)] decimal Amount);
    [HttpGet] public async Task<ActionResult<ReferralSummary>> Get(CancellationToken ct) => Ok(await referrals.GetAsync(ct));
    [HttpPost("code")] public async Task<ActionResult<ReferralSummary>> CreateCode(CancellationToken ct) => Ok(await referrals.CreateCodeAsync(ct));
    [HttpPost("claim")] public async Task<ActionResult<ReferralSummary>> Claim(ClaimRequest request, CancellationToken ct) => Ok(await referrals.ClaimAsync(request.Code, ct));
    [HttpPost("quote")] public async Task<IActionResult> Quote(QuoteRequest request, CancellationToken ct) => Ok(new { discount = await referrals.QuoteAsync(request.VoucherId, request.Purpose, request.Amount, ct) });
}
