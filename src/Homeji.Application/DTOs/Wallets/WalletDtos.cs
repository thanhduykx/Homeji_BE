using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Wallets;

public sealed record WalletDto(
    Guid UserId,
    decimal Balance,
    decimal TotalDeposited,
    decimal TotalSpent,
    decimal TotalEarned,
    bool IsActivated,
    decimal MinimumTopUp,
    decimal MaximumTopUp,
    decimal MinimumWithdrawalReserve,
    DateTimeOffset? UpdatedAt);

public sealed record WalletTransactionDto(
    Guid Id,
    WalletTransactionKind Kind,
    decimal Amount,
    decimal BalanceAfter,
    Guid ReferenceId,
    string Description,
    DateTimeOffset CreatedAt);

public sealed record CreateWalletWithdrawalDto(
    decimal Amount,
    string BankName,
    string AccountNumber,
    string AccountHolder);

public sealed record ReviewWalletWithdrawalDto(string? Note);

public sealed record WalletWithdrawalDto(
    Guid Id,
    Guid UserId,
    decimal Amount,
    string BankName,
    string AccountNumber,
    string AccountHolder,
    WalletWithdrawalStatus Status,
    string? AdminNote,
    Guid? ProcessedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);
