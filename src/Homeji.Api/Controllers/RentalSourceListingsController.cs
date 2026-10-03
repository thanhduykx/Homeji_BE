using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/rental-source-listings")]
public sealed class RentalSourceListingsController(IRentalSourceListingService service) : ControllerBase
{
    [HttpGet]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicSearch)]
    [ProducesResponseType<IReadOnlyList<RentalSourceListingDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RentalSourceListingDto>>> Search(
        [FromQuery] string? keyword, [FromQuery] string? district,
        [FromQuery] decimal? minPrice, [FromQuery] decimal? maxPrice,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        Ok(await service.SearchAsync(new RentalSourceSearchDto(keyword, district, minPrice, maxPrice, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [ProducesResponseType<RentalSourceListingDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<RentalSourceListingDto>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));
}
