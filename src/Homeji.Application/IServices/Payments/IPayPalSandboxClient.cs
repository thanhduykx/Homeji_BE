using Homeji.Application.DTOs.Payments;

namespace Homeji.Application.IServices.Payments;

public interface IPayPalSandboxClient
{
    bool IsConfigured { get; }

    Task<(string OrderId, Uri ApprovalUrl)> CreateAsync(
        Uri returnUrl, Uri cancelUrl, CancellationToken cancellationToken);

    Task<PayPalSandboxCapture> CaptureAsync(string orderId, CancellationToken cancellationToken);
}
