namespace Homeji.Api.Views.Payments;

public sealed record PayOsWebhookViewModel(
    string? Code,
    string? Desc,
    bool Success,
    PayOsWebhookDataViewModel? Data,
    string? Signature);

public sealed record PayOsWebhookDataViewModel(
    long OrderCode,
    int Amount,
    string? Description,
    string? AccountNumber,
    string? Reference,
    string? TransactionDateTime,
    string? Currency,
    string? PaymentLinkId,
    string? Code,
    string? Desc,
    string? CounterAccountBankId,
    string? CounterAccountBankName,
    string? CounterAccountName,
    string? CounterAccountNumber,
    string? VirtualAccountName,
    string? VirtualAccountNumber);
