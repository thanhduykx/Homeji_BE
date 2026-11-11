using Homeji.Api.Caching;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.DTOs.Subscriptions;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.IServices.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

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
    [OutputCache(PolicyName = PublicCachePolicies.Pricing)]
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

    [HttpPost("premium/{packageCode}/wallet")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentDto>> PurchaseWithWallet(
        string packageCode, [FromBody] WalletPremiumPaymentDto request,
        [FromServices] IPremiumWalletPaymentService walletPayments, CancellationToken cancellationToken)
        => Ok(await walletPayments.PurchaseAsync(packageCode, request, cancellationToken));

    [HttpPost("premium/{packageCode}/momo/create")]
    [ProducesResponseType<MomoPaymentResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MomoPaymentResponseDto>> CreatePremiumMomoPayment(
        string packageCode,
        [FromQuery] decimal? expectedAmount, CancellationToken cancellationToken)
    {
        return Ok(await _subscriptions.CreatePremiumMomoPaymentAsync(packageCode, expectedAmount, cancellationToken));
    }

    [HttpPost("premium/{packageCode}/payos/create")]
    [ProducesResponseType<PayOsPaymentResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PayOsPaymentResponseDto>> CreatePremiumPayOsPayment(
        string packageCode,
        [FromQuery] decimal? expectedAmount, CancellationToken cancellationToken)
    {
        return Ok(await _subscriptions.CreatePremiumPayOsPaymentAsync(packageCode, expectedAmount, cancellationToken));
    }
}
