using Homeji.Application.DTOs.Payments;
namespace Homeji.Application.IServices.Payments;

public interface IPremiumWalletPaymentService
{
    Task<PaymentDto> PurchaseAsync(string packageCode, WalletPremiumPaymentDto request, CancellationToken cancellationToken = default);
}
