using Homeji.Api.Caching;
using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/rental-source-listings")]
public sealed class RentalSourceListingsController(IRentalSourceListingService service) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [OutputCache(PolicyName = PublicCachePolicies.Catalog)]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicSearch)]
    [ProducesResponseType<IReadOnlyList<RentalSourceListingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalSourceListingDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] string? district,
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? propertyCategory = null, CancellationToken cancellationToken = default) =>
        Ok(await service.SearchAsync(new RentalSourceSearchDto(keyword, district, minPrice, maxPrice, page, pageSize, propertyCategory), cancellationToken));

    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [OutputCache(PolicyName = PublicCachePolicies.Catalog)]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [ProducesResponseType<RentalSourceListingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalSourceListingDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [Authorize]
    [HttpGet("{id:guid}/contact")]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [ProducesResponseType<RentalSourceContactDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RentalSourceContactDto>> GetContact(Guid id, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "private, no-store";
        return Ok(await service.GetContactAsync(id, cancellationToken));
    }
}
