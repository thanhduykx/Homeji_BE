using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Homeji.Api.RateLimiting;
namespace Homeji.Api.Controllers;

[ApiController, Route("api/search-interests")]
[EnableRateLimiting(RateLimitingPolicyNames.PublicSearch)]
public sealed class SearchInterestsController(ISearchInterestService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<InterestOverview>> Get(CancellationToken ct) => Ok(await service.GetAsync(ct));
    [HttpPut("settings")] public async Task<ActionResult<InterestOverview>> Settings(InterestSettings request, CancellationToken ct) => Ok(await service.SettingsAsync(request, ct));
    [HttpPost("observations")] public async Task<ActionResult<InterestOverview>> Observe(ObserveInterest request, CancellationToken ct) => Ok(await service.ObserveAsync(request, ct));
    [HttpPost("following")] public async Task<ActionResult<InterestOverview>> Follow(InterestCriteria request, CancellationToken ct) => Ok(await service.FollowAsync(request, ct));
    [HttpDelete("following/{id:guid}")] public async Task<ActionResult<InterestOverview>> Remove(Guid id, CancellationToken ct) => Ok(await service.RemoveAsync(id, ct));
    [HttpPost("dismiss")] public async Task<ActionResult<InterestOverview>> Dismiss([FromBody] string area, CancellationToken ct) => Ok(await service.DismissAsync(area, ct));
    [HttpDelete("history")] public async Task<ActionResult<InterestOverview>> Clear(CancellationToken ct) => Ok(await service.ClearAsync(ct));
    [HttpPost("viewed/{postId:guid}")] public async Task<IActionResult> Viewed(Guid postId, CancellationToken ct) { await service.ViewedAsync(postId, ct); return NoContent(); }
    [HttpPost("ai-ranking"), EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)] public async Task<ActionResult<InterestOverview>> Rank(CancellationToken ct) => Ok(await service.RankAsync(ct));
    [HttpPost("ai-proposal"), EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)] public async Task<ActionResult<AiInterestProposal>> Infer(CancellationToken ct) => Ok(await service.InferAsync(ct));
}
