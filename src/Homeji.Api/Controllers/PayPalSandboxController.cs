using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Homeji.Api.RateLimiting;
using Homeji.Application.IServices.Payments;
using Homeji.Application.Services.Common;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Homeji.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/payments/paypal-sandbox")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PayPalSandboxController(
    IHostEnvironment environment,
    IOptions<PayPalSandboxOptions> settings,
    IDataProtectionProvider protectionProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAvailability(
        [FromServices] UserContext user,
        [FromServices] IPayPalSandboxClient paypal,
        CancellationToken cancellationToken) =>
        await CanUseAsync(user, paypal, cancellationToken) ? NoContent() : NotFound();

    [HttpGet("wallets")]
    public async Task<IActionResult> GetWallets(
        [FromServices] UserContext user,
        [FromServices] IPayPalSandboxClient paypal,
        CancellationToken cancellationToken)
    {
        if (!await CanUseAsync(user, paypal, cancellationToken))
        {
            return NotFound();
        }

        var googlePay = IsWalletEnabled("google-pay");
        var applePay = IsWalletEnabled("apple-pay");
        return googlePay || applePay ? Ok(new { clientId = paypal.ClientId, googlePay, applePay }) : NoContent();
    }

    [HttpPost("wallets/{wallet}/orders")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<IActionResult> CreateWalletOrder(
        string wallet,
        [FromServices] UserContext user,
        [FromServices] IPayPalSandboxClient paypal,
        CancellationToken cancellationToken)
    {
        if (!IsWalletEnabled(wallet) || !await CanUseAsync(user, paypal, cancellationToken))
        {
            return NotFound();
        }

        var orderId = await paypal.CreateWalletOrderAsync(cancellationToken);
        var ticket = CreateProtector(user.GetRequiredUserId()).Protect($"{wallet}:{orderId}", TimeSpan.FromHours(1));
        return Ok(new { orderId, ticket });
    }

    [HttpPost("orders")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<IActionResult> Create(
        [FromServices] UserContext user,
        [FromServices] IPayPalSandboxClient paypal,
        CancellationToken cancellationToken)
    {
        if (!await CanUseAsync(user, paypal, cancellationToken))
        {
            return NotFound();
        }

        var userId = user.GetRequiredUserId();
        var origin = new Uri(settings.Value.FrontendOrigin);
        var order = await paypal.CreateAsync(
            new Uri(origin, "/payments/sandbox?paypal=return"),
            new Uri(origin, "/payments/sandbox?paypal=cancel"),
            cancellationToken);
        var ticket = CreateProtector(userId).Protect(order.OrderId, TimeSpan.FromHours(1));
        return Ok(new { orderId = order.OrderId, approvalUrl = order.ApprovalUrl, ticket });
    }

    [HttpPost("capture")]
    [EnableRateLimiting(RateLimitingPolicyNames.CostlyOperations)]
    public async Task<IActionResult> Capture(
        [FromBody] CaptureRequest request,
        [FromServices] UserContext user,
        [FromServices] IPayPalSandboxClient paypal,
        CancellationToken cancellationToken)
    {
        if (!await CanUseAsync(user, paypal, cancellationToken))
        {
            return NotFound();
        }

        string orderId;
        try
        {
            orderId = CreateProtector(user.GetRequiredUserId()).Unprotect(request.Ticket);
        }
        catch (CryptographicException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound,
                detail: "Đơn PayPal Sandbox đã hết hạn hoặc không thuộc tài khoản của bạn.");
        }

        string? wallet = null;
        var separator = orderId.IndexOf(':');
        if (separator >= 0)
        {
            wallet = orderId[..separator];
            orderId = orderId[(separator + 1)..];
            if (!IsWalletEnabled(wallet))
            {
                return NotFound();
            }
        }

        return Ok(await paypal.CaptureAsync(orderId, cancellationToken, wallet));
    }

    private bool IsWalletEnabled(string wallet) => wallet switch
    {
        "google-pay" => settings.Value.GooglePayEnabled,
        "apple-pay" => settings.Value.ApplePayEnabled
            && Uri.TryCreate(settings.Value.FrontendOrigin, UriKind.Absolute, out var origin)
            && origin.Scheme == Uri.UriSchemeHttps && !origin.IsLoopback,
        _ => false,
    };

    private async Task<bool> CanUseAsync(
        UserContext user, IPayPalSandboxClient paypal, CancellationToken cancellationToken)
    {
        var options = settings.Value;
        if (!options.Enabled
            || !Uri.TryCreate(options.FrontendOrigin, UriKind.Absolute, out var origin)
            || origin.AbsolutePath != "/" || !string.IsNullOrEmpty(origin.UserInfo)
            || !string.IsNullOrEmpty(origin.Query) || !string.IsNullOrEmpty(origin.Fragment)
            || !paypal.IsConfigured)
        {
            return false;
        }

        if (environment.IsDevelopment() && origin.IsLoopback)
        {
            return origin.Scheme is "http" or "https";
        }

        return environment.IsProduction() && origin.Scheme == Uri.UriSchemeHttps && !origin.IsLoopback
            && (await user.GetRequiredProfileAsync(cancellationToken)).Role == UserRole.Admin;
    }

    private ITimeLimitedDataProtector CreateProtector(Guid userId) =>
        protectionProvider.CreateProtector("Homeji.PayPalSandbox.Order.v1", userId.ToString("N")).ToTimeLimitedDataProtector();

    public sealed class CaptureRequest
    {
        [Required]
        [StringLength(1024)]
        public string Ticket { get; init; } = "";
    }
}
