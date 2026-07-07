using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Payments;

public sealed record MomoPaymentResponseDto(
    Guid PaymentId,
    string OrderCode,
    string RequestId,
    decimal Amount,
    PaymentStatus Status,
    string? PayUrl,
    string? Deeplink,
    string? QrCodeUrl,
    string? ProviderMessage);
