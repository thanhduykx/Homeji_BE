using Homeji.Application.DTOs.Payments;
using Homeji.Application.DTOs.Subscriptions;

namespace Homeji.Application.IServices.Subscriptions;

public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionPackageDto>> GetPackagesAsync(CancellationToken cancellationToken = default);

    Task<MySubscriptionDto> GetMySubscriptionAsync(CancellationToken cancellationToken = default);

    Task<MomoPaymentResponseDto> CreatePremiumMomoPaymentAsync(CancellationToken cancellationToken = default);

    Task<PayOsPaymentResponseDto> CreatePremiumPayOsPaymentAsync(CancellationToken cancellationToken = default);
}
