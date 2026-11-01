using Homeji.Application.IServices.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/subscriptions/price-announcements")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PremiumPricingController(IPremiumPricing pricing) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Announcements(CancellationToken cancellationToken) => Ok(await pricing.GetAnnouncementsAsync(cancellationToken));

    [HttpPost("{code}"), Authorize]
    public async Task<IActionResult> Change(string code, ChangePremiumPriceRequest request, CancellationToken cancellationToken) => Ok(await pricing.ChangeAsync(code, request, cancellationToken));
}
