using Homeji.Application.DTOs.Payments;

namespace Homeji.Application.IServices.Payments;

public interface IPayPalSandboxClient
{
    bool IsConfigured { get; }
    string ClientId { get; }

    Task<(string OrderId, Uri ApprovalUrl)> CreateAsync(
        Uri returnUrl, Uri cancelUrl, CancellationToken cancellationToken);

    Task<string> CreateWalletOrderAsync(CancellationToken cancellationToken);
    Task<PayPalSandboxCapture> CaptureAsync(string orderId, CancellationToken cancellationToken, string? wallet = null);
}
