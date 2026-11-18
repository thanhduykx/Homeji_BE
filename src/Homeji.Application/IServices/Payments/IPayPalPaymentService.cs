using Homeji.Application.DTOs.Payments;

namespace Homeji.Application.IServices.Payments;

public interface IPayPalPaymentService
{
    Task<PayPalCheckoutConfigDto> GetConfigAndQuoteAsync(decimal? amountVnd, CancellationToken cancellationToken = default);
    Task<PayPalPaymentResponseDto> CreateWalletTopUpAsync(CreatePayPalPaymentDto request, CancellationToken cancellationToken = default);
    Task<PayPalPaymentResponseDto> CreatePremiumPaymentAsync(string packageCode, decimal? expectedAmount = null, CancellationToken cancellationToken = default);
    Task<PayPalCheckoutConfigDto> GetPaymentWalletsAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<PayPalWalletOrderDto> CreatePaymentWalletOrderAsync(Guid paymentId, string wallet, CancellationToken cancellationToken = default);
    Task<PaymentDto> CapturePaymentAsync(Guid paymentId, CapturePayPalPaymentDto request, CancellationToken cancellationToken = default);
}
