namespace Homeji.Api.Views.Payments;

public sealed record CreatePayOsPaymentViewModel(
    decimal Amount,
    string? Description);
