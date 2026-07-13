using Homeji.Api.Mappers;
using Homeji.Api.Views.Payments;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IServices.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Homeji.Domain.Enums;

namespace Homeji.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
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
    public IActionResult MomoReturn()
    {
        return Ok(new { message = "MoMo returned to Homeji API. Frontend should query order status." });
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
    [HttpPost("payos/webhook")]
    [ProducesResponseType<PaymentDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> HandlePayOsWebhook(
        [FromBody] PayOsWebhookViewModel request,
        CancellationToken cancellationToken)
    {
        var payment = await _paymentService.HandlePayOsWebhookAsync(PaymentViewMapper.ToDto(request), cancellationToken);
        return payment is null
            ? Ok(new { message = "PayOS webhook accepted. Matching payment was not found." })
            : Ok(payment);
    }

    [AllowAnonymous]
    [HttpGet("payos/return")]
    public IActionResult PayOsReturn()
    {
        return Ok(new { message = "PayOS returned to Homeji API. Frontend should query order status." });
    }

    [AllowAnonymous]
    [HttpGet("payos/cancel")]
    public IActionResult PayOsCancel()
    {
        return Ok(new { message = "PayOS payment was cancelled." });
    }
}
