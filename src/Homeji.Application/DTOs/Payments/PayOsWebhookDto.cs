namespace Homeji.Application.DTOs.Payments;

public sealed record PayOsWebhookDto(
    string? Code,
    string? Desc,
    bool Success,
    PayOsWebhookDataDto? Data,
    string? Signature);

public sealed record PayOsWebhookDataDto(
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
