namespace Homeji.Application.DTOs.Payments;

public sealed record CreatePayOsPaymentDto(
    decimal Amount,
    string? Description);
