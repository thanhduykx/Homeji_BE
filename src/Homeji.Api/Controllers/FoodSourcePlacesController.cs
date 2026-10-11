using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IServices.Marketplace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/food-source-places")]
public sealed class FoodSourcePlacesController(IFoodSourcePlaceService service) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [ProducesResponseType<IReadOnlyList<FoodSourcePlaceDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FoodSourcePlaceDto>>> List(CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(cancellationToken));
}
