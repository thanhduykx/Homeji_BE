using Homeji.Api.Mappers;
using Homeji.Api.RateLimiting;
using Homeji.Api.Views.Payments;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IServices.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Homeji.Domain.Enums;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly PaymentRedirectOptions _redirectOptions;

    public PaymentsController(
        IPaymentService paymentService,
        IOptions<PaymentRedirectOptions> redirectOptions)
    {
        _paymentService = paymentService;
        _redirectOptions = redirectOptions.Value;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PaymentDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> GetHistory(
        [FromQuery] PaymentStatus? status = null,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _paymentService.GetMyPaymentHistoryAsync(status, take, cancellationToken));
    }

    [HttpGet("{paymentId:guid}")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentDto>> GetPaymentById(
        Guid paymentId,
        CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.GetPaymentByIdAsync(paymentId, cancellationToken));
    }

    [HttpGet("orders/{orderCode}")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PaymentDto>> GetPaymentByOrderCode(
        string orderCode,
        CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.GetPaymentByOrderCodeAsync(orderCode, cancellationToken));
    }

    [HttpPost("momo/create")]
    [ProducesResponseType<MomoPaymentResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MomoPaymentResponseDto>> CreateMomoPayment(
        [FromBody] CreateMomoPaymentViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.CreateMomoPaymentAsync(PaymentViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PaymentWebhook)]
    [HttpPost("momo/ipn")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentDto>> HandleMomoIpn(
        [FromBody] MomoIpnViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.HandleMomoIpnAsync(PaymentViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("momo/return")]
    public IActionResult MomoReturn(
        [FromQuery] string? orderId,
        [FromQuery] int? resultCode,
        [FromQuery] string? message)
    {
        return RedirectToFrontend(
            "momo",
            orderId,
            new Dictionary<string, string?>
            {
                ["resultCode"] = resultCode?.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["message"] = message,
            });
    }

    [HttpPost("payos/create")]
    [ProducesResponseType<PayOsPaymentResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayOsPaymentResponseDto>> CreatePayOsPayment(
        [FromBody] CreatePayOsPaymentViewModel request,
        CancellationToken cancellationToken)
    {
        return Ok(await _paymentService.CreatePayOsPaymentAsync(PaymentViewMapper.ToDto(request), cancellationToken));
    }

    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingPolicyNames.PaymentWebhook)]
    [HttpPost("payos/webhook")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> HandlePayOsWebhook(
        [FromBody] PayOsWebhookViewModel request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentService.HandlePayOsWebhookAsync(PaymentViewMapper.ToDto(request), cancellationToken);
        return payment is null
            ? Ok(new { message = "Đã nhận webhook PayOS. Không tìm thấy giao dịch tương ứng." })
            : Ok(payment);
    }

    [AllowAnonymous]
    [HttpGet("payos/return")]
    public IActionResult PayOsReturn(
        [FromQuery] string? code,
        [FromQuery] string? id,
        [FromQuery] bool? cancel,
        [FromQuery] string? status,
        [FromQuery] string? orderCode)
    {
        return RedirectToFrontend(
            "payos",
            orderCode,
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["paymentLinkId"] = id,
                ["cancel"] = cancel?.ToString().ToLowerInvariant(),
                ["providerStatus"] = status,
            });
    }

    [AllowAnonymous]
    [HttpGet("payos/cancel")]
    public IActionResult PayOsCancel(
        [FromQuery] string? code,
        [FromQuery] string? id,
        [FromQuery] bool? cancel,
        [FromQuery] string? status,
        [FromQuery] string? orderCode)
    {
        return RedirectToFrontend(
            "payos",
            orderCode,
            new Dictionary<string, string?>
            {
                ["code"] = code,
                ["paymentLinkId"] = id,
                ["cancel"] = (cancel ?? true).ToString().ToLowerInvariant(),
                ["providerStatus"] = status ?? "CANCELLED",
            });
    }

    [AllowAnonymous]
    [HttpGet("paypal/quote")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<PayPalCheckoutConfigDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayPalCheckoutConfigDto>> GetPayPalQuote(
        [FromQuery] decimal? amount,
        [FromServices] IPayPalPaymentService payPalPayments,
        CancellationToken cancellationToken)
    {
        return Ok(await payPalPayments.GetConfigAndQuoteAsync(amount, cancellationToken));
    }

    [HttpPost("paypal/create")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    [ProducesResponseType<PayPalPaymentResponseDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayPalPaymentResponseDto>> CreatePayPalPayment(
        [FromBody] CreatePayPalPaymentDto request,
        [FromServices] IPayPalPaymentService payPalPayments,
        CancellationToken cancellationToken)
    {
        return Ok(await payPalPayments.CreateWalletTopUpAsync(request, cancellationToken));
    }

    [HttpGet("{paymentId:guid}/paypal/wallets")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<PayPalCheckoutConfigDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayPalCheckoutConfigDto>> GetPayPalPaymentWallets(
        Guid paymentId,
        [FromServices] IPayPalPaymentService payPalPayments,
        CancellationToken cancellationToken)
    {
        return Ok(await payPalPayments.GetPaymentWalletsAsync(paymentId, cancellationToken));
    }

    [HttpPost("{paymentId:guid}/paypal/wallets/{wallet}/orders")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    [ProducesResponseType<PayPalWalletOrderDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PayPalWalletOrderDto>> CreatePayPalWalletOrder(
        Guid paymentId,
        string wallet,
        [FromServices] IPayPalPaymentService payPalPayments,
        CancellationToken cancellationToken)
    {
        return Ok(await payPalPayments.CreatePaymentWalletOrderAsync(paymentId, wallet, cancellationToken));
    }

    [HttpPost("{paymentId:guid}/paypal/capture")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PaymentDto>> CapturePayPalPayment(
        Guid paymentId,
        [FromBody] CapturePayPalPaymentDto? request,
        [FromServices] IPayPalPaymentService payPalPayments,
        CancellationToken cancellationToken)
    {
        return Ok(await payPalPayments.CapturePaymentAsync(paymentId, request ?? new(), cancellationToken));
    }

    [AllowAnonymous]
    [HttpGet("paypal/return")]
    public IActionResult PayPalReturn(
        [FromQuery] string? orderCode,
        [FromQuery] string? token)
    {
        return RedirectToFrontend(
            "paypal",
            orderCode,
            new Dictionary<string, string?>
            {
                ["token"] = token,
            });
    }

    [AllowAnonymous]
    [HttpGet("paypal/cancel")]
    public IActionResult PayPalCancel(
        [FromQuery] string? orderCode,
        [FromQuery] string? token)
    {
        return RedirectToFrontend(
            "paypal",
            orderCode,
            new Dictionary<string, string?>
            {
                ["token"] = token,
                ["cancel"] = "true",
                ["providerStatus"] = "CANCELLED",
            });
    }

    private RedirectResult RedirectToFrontend(
        string provider,
        string? orderCode,
        IReadOnlyDictionary<string, string?> providerParameters)
    {
        var parameters = new Dictionary<string, string?>(providerParameters, StringComparer.Ordinal)
        {
            ["provider"] = provider,
            ["orderCode"] = orderCode,
        };
        var populatedParameters = parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Value))
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Value, StringComparer.Ordinal);

        return Redirect(QueryHelpers.AddQueryString(_redirectOptions.FrontendPaymentUrl, populatedParameters));
    }
}
