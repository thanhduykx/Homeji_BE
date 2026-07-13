using Homeji.Application.DTOs.Payments;
using Homeji.Application.DTOs.Subscriptions;
using Homeji.Application.IServices.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _subscriptions;

    public SubscriptionsController(ISubscriptionService subscriptions)
    {
        _subscriptions = subscriptions;
    }

    [AllowAnonymous]
    [HttpGet("packages")]
    [ProducesResponseType<IReadOnlyList<SubscriptionPackageDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPackageDto>>> GetPackages(
        CancellationToken cancellationToken)
    {
        return Ok(await _subscriptions.GetPackagesAsync(cancellationToken));
    }

    [HttpGet("me")]
    [ProducesResponseType<MySubscriptionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MySubscriptionDto>> GetMySubscription(
        CancellationToken cancellationToken)
    {
        return Ok(await _subscriptions.GetMySubscriptionAsync(cancellationToken));
    }

    [HttpPost("premium/{packageCode}/momo/create")]
    [ProducesResponseType<MomoPaymentResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MomoPaymentResponseDto>> CreatePremiumMomoPayment(
        string packageCode,
        CancellationToken cancellationToken)
    {
        return Ok(await _subscriptions.CreatePremiumMomoPaymentAsync(packageCode, cancellationToken));
    }

    [HttpPost("premium/{packageCode}/payos/create")]
    [ProducesResponseType<PayOsPaymentResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PayOsPaymentResponseDto>> CreatePremiumPayOsPayment(
        string packageCode,
        CancellationToken cancellationToken)
    {
        return Ok(await _subscriptions.CreatePremiumPayOsPaymentAsync(packageCode, cancellationToken));
    }
}
