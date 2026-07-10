using Homeji.Application.DTOs.Payments;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Payments;

public static class PaymentMapper
{
    public static PaymentDto ToDto(PaymentTransaction payment)
    {
        return new PaymentDto(
            payment.Id,
            payment.UserId,
            payment.Method,
            payment.Status,
            payment.Amount,
            payment.Purpose,
            payment.PackageCode,
            payment.OrderCode,
            payment.RequestId,
            payment.Description,
            payment.PaymentUrl,
            payment.Deeplink,
            payment.QrCodeUrl,
            payment.QrCode,
            payment.QrDataUrl,
            payment.ExternalTransactionId,
            payment.ProviderMessage,
            payment.CreatedAt,
            payment.UpdatedAt,
            payment.PaidAt);
    }
}
