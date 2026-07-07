using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Payments;

public sealed record PayOsPaymentResponseDto(
    Guid PaymentId,
    string OrderCode,
    decimal Amount,
    PaymentStatus Status,
    string? CheckoutUrl,
    string? QrCode,
    string? ProviderMessage);
