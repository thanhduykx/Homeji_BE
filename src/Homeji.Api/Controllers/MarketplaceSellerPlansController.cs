using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IServices.Marketplace;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/marketplace-seller-plans")]
public sealed class MarketplaceSellerPlansController : ControllerBase
{
    private readonly IMarketplaceSellerPlanService _plans;

    public MarketplaceSellerPlansController(IMarketplaceSellerPlanService plans)
    {
        _plans = plans;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MarketplaceSellerPlanDto>>> GetPlans(CancellationToken cancellationToken) =>
        Ok(await _plans.GetPlansAsync(cancellationToken));

    [HttpGet("mine")]
    public async Task<ActionResult<MarketplaceSellerSubscriptionDto>> GetMine(CancellationToken cancellationToken) =>
        Ok(await _plans.GetMineAsync(cancellationToken));

    [HttpPost("{packageCode}/purchase")]
    public async Task<ActionResult<MarketplaceSellerSubscriptionDto>> Purchase(
        string packageCode,
        CancellationToken cancellationToken) =>
        Ok(await _plans.PurchaseAsync(packageCode, cancellationToken));
}
