namespace Homeji.Application.DTOs.Payments;

public sealed record CreateMomoPaymentDto(
    decimal Amount,
    string? Description);
