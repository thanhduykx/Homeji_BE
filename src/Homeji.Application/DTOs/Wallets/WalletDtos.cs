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
    DateTimeOffset? UpdatedAt);

public sealed record WalletTransactionDto(
    Guid Id,
    WalletTransactionKind Kind,
    decimal Amount,
    decimal BalanceAfter,
    Guid ReferenceId,
    string Description,
    DateTimeOffset CreatedAt);
