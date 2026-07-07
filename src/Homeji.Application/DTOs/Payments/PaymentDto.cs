using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Payments;

public sealed record PaymentDto(
    Guid Id,
    Guid UserId,
    PaymentMethod Method,
    PaymentStatus Status,
    decimal Amount,
    string OrderCode,
    string? RequestId,
    string Description,
    string? PaymentUrl,
    string? Deeplink,
    string? QrCodeUrl,
    string? QrCode,
    string? QrDataUrl,
    string? ExternalTransactionId,
    string? ProviderMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PaidAt);
