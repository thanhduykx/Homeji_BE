using Homeji.Application.DTOs.Payments;
using Homeji.Domain.Enums;

namespace Homeji.Application.IServices.Payments;

public interface IPaymentService
{
    Task<PaymentDto> GetPaymentByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);

    Task<PaymentDto> GetPaymentByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentDto>> GetMyPaymentHistoryAsync(
        PaymentStatus? status,
        int take,
        CancellationToken cancellationToken = default);

    Task<MomoPaymentResponseDto> CreateMomoPaymentAsync(CreateMomoPaymentDto request, CancellationToken cancellationToken = default);

    Task<MomoPaymentResponseDto> CreatePremiumMomoPaymentAsync(string packageCode, CancellationToken cancellationToken = default);

    Task<PaymentDto> HandleMomoIpnAsync(MomoIpnDto request, CancellationToken cancellationToken = default);

    Task<PayOsPaymentResponseDto> CreatePayOsPaymentAsync(CreatePayOsPaymentDto request, CancellationToken cancellationToken = default);

    Task<PayOsPaymentResponseDto> CreatePremiumPayOsPaymentAsync(string packageCode, CancellationToken cancellationToken = default);

    Task<PaymentDto?> HandlePayOsWebhookAsync(PayOsWebhookDto request, CancellationToken cancellationToken = default);
}
