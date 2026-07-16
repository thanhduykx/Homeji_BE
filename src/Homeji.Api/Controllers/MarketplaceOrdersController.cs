using Homeji.Application.DTOs.MarketplaceOrders;
using Homeji.Application.IServices.MarketplaceOrders;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/marketplace-orders")]
public sealed class MarketplaceOrdersController : ControllerBase
{
    private readonly IMarketplaceOrderService _orders;
    public MarketplaceOrdersController(IMarketplaceOrderService orders) => _orders = orders;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MarketplaceOrderDto>>> GetMine(CancellationToken cancellationToken) =>
        Ok(await _orders.GetMineAsync(cancellationToken));

    [HttpPost("/api/marketplace-posts/{postId:guid}/orders")]
    public async Task<ActionResult<MarketplaceOrderDto>> Create(
        Guid postId,
        [FromBody] CreateMarketplaceOrderDto request,
        CancellationToken cancellationToken)
    {
        var result = await _orders.CreateAsync(postId, request, cancellationToken);
        return Created($"/api/marketplace-orders/{result.Id}", result);
    }

    [HttpPost("cart")]
    public async Task<ActionResult<IReadOnlyList<MarketplaceOrderDto>>> CreateCart(
        [FromBody] CreateMarketplaceCartOrderDto request,
        CancellationToken cancellationToken)
    {
        var result = await _orders.CreateCartAsync(request, cancellationToken);
        return Created("/api/marketplace-orders", result);
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<MarketplaceOrderDto>> Accept(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.AcceptAsync(id, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<MarketplaceOrderDto>> Reject(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.RejectAsync(id, cancellationToken));

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<MarketplaceOrderDto>> Cancel(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.CancelAsync(id, cancellationToken));

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<MarketplaceOrderDto>> Complete(Guid id, CancellationToken cancellationToken) =>
        Ok(await _orders.CompleteAsync(id, cancellationToken));
}
