using Homeji.Api.RateLimiting;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/rentals")]
[EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
public sealed class PublicRentalsController(IPublicRentalCatalog catalog) : ControllerBase
{
    // Indexing reads must not increment views, record activity, or expose owner-only fields.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PublicRentalPage>> Get(Guid id, CancellationToken cancellationToken)
    {
        var page = await catalog.GetAsync(id, cancellationToken);
        return page is null ? NotFound() : Ok(page);
    }

    [HttpGet("sitemap")]
    public async Task<ActionResult<PublicRentalSitemap>> Sitemap([FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        if (page is < 1 or > 50_000) return BadRequest();
        return Ok(await catalog.GetSitemapAsync(page, cancellationToken));
    }
}
