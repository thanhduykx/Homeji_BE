using Homeji.Application.DTOs.Payments;

namespace Homeji.Application.IServices.Payments;

public interface IPaymentService
{
    Task<PaymentDto> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<PaymentDto> GetPaymentByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);

    Task<MomoPaymentResponseDto> CreateMomoPaymentAsync(CreateMomoPaymentDto request, CancellationToken cancellationToken = default);

    Task<MomoPaymentResponseDto> CreatePremiumMomoPaymentAsync(CancellationToken cancellationToken = default);

    Task<PaymentDto> HandleMomoIpnAsync(MomoIpnDto request, CancellationToken cancellationToken = default);

    Task<PayOsPaymentResponseDto> CreatePayOsPaymentAsync(CreatePayOsPaymentDto request, CancellationToken cancellationToken = default);

    Task<PayOsPaymentResponseDto> CreatePremiumPayOsPaymentAsync(CancellationToken cancellationToken = default);

    Task<PaymentDto?> HandlePayOsWebhookAsync(PayOsWebhookDto request, CancellationToken cancellationToken = default);
}
