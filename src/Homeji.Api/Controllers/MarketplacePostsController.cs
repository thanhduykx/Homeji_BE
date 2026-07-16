using Homeji.Api.RateLimiting;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IServices.Marketplace;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/marketplace-posts")]
public sealed class MarketplacePostsController : ControllerBase
{
    private readonly IMarketplacePostService _marketplaceService;

    public MarketplacePostsController(IMarketplacePostService marketplaceService)
    {
        _marketplaceService = marketplaceService;
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicSearch)]
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<MarketplacePostDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MarketplacePostDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] MarketplaceListingType? listingType,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] decimal? latitude,
        [FromQuery] decimal? longitude,
        [FromQuery] decimal? radiusKm,
        [FromQuery] Guid? nearRentalPostId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _marketplaceService.SearchAsync(new MarketplaceSearchDto(
            keyword,
            category,
            listingType,
            minPrice,
            maxPrice,
            latitude,
            longitude,
            radiusKm,
            nearRentalPostId,
            page,
            pageSize), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PublicRead)]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MarketplacePostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketplacePostDto>> GetDetail(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _marketplaceService.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType<MarketplacePostDto>(StatusCodes.Status201Created)]
    public async Task<ActionResult<MarketplacePostDto>> Create(
        [FromBody] UpsertMarketplacePostDto request,
        CancellationToken cancellationToken)
    {
        var post = await _marketplaceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetDetail), new { id = post.Id }, post);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<MarketplacePostDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MarketplacePostDto>> Update(
        Guid id,
        [FromBody] UpsertMarketplacePostDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _marketplaceService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPost("{id:guid}/sold")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkSold(Guid id, CancellationToken cancellationToken)
    {
        await _marketplaceService.MarkSoldAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await _marketplaceService.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }
}
