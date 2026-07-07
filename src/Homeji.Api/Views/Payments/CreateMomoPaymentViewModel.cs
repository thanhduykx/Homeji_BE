namespace Homeji.Api.Views.Payments;

public sealed record CreateMomoPaymentViewModel(
    decimal Amount,
    string? Description);
