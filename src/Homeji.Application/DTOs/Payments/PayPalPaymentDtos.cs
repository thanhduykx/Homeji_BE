using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Payments;

public sealed record CreatePayPalPaymentDto(decimal Amount, string? Description = null);

public sealed record PayPalCurrencyQuoteDto(
    decimal AmountVnd,
    decimal ExchangeRateVnd,
    string RateSource,
    DateTimeOffset RateUpdatedAt,
    decimal BaseUsd,
    decimal BankFeePercent,
    decimal BankFeeUsd,
    decimal GatewayFeePercent,
    decimal GatewayFixedFeeUsd,
    decimal GatewayFeeUsd,
    decimal TotalFeeUsd,
    decimal TotalUsd);

public sealed record PayPalCheckoutConfigDto(
    bool Enabled,
    string Mode,
    string? ClientId,
    bool GooglePay,
    bool ApplePay,
    PayPalCurrencyQuoteDto? Quote);

public sealed record PayPalPaymentResponseDto(
    Guid PaymentId,
    string OrderCode,
    string PayPalOrderId,
    decimal Amount,
    PaymentStatus Status,
    string? ApprovalUrl,
    string? ProviderMessage,
    PayPalCurrencyQuoteDto Quote);

public sealed record PayPalWalletOrderDto(string OrderId, string TotalUsd, string Ticket);

public sealed record CapturePayPalPaymentDto(string? Ticket = null, string? PayPalOrderId = null);
