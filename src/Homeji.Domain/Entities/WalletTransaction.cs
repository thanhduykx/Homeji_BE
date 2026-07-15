using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class WalletTransaction
{
    public const int MaxDescriptionLength = 300;

    private WalletTransaction()
    {
        Description = null!;
    }

    public WalletTransaction(
        Guid walletUserId,
        WalletTransactionKind kind,
        decimal amount,
        decimal balanceAfter,
        Guid referenceId,
        string description,
        DateTimeOffset createdAt)
    {
        if (walletUserId == Guid.Empty || referenceId == Guid.Empty)
        {
            throw new DomainException("Wallet transaction identifiers must not be empty.");
        }

        if (!Enum.IsDefined(kind) || amount == 0 || decimal.Truncate(amount) != amount)
        {
            throw new DomainException("Wallet transaction amount or kind is invalid.");
        }

        Id = Guid.NewGuid();
        WalletUserId = walletUserId;
        Kind = kind;
        Amount = amount;
        BalanceAfter = balanceAfter;
        ReferenceId = referenceId;
        Description = NormalizeDescription(description);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid WalletUserId { get; private set; }
    public WalletTransactionKind Kind { get; private set; }
    public decimal Amount { get; private set; }
    public decimal BalanceAfter { get; private set; }
    public Guid ReferenceId { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private static string NormalizeDescription(string value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException("Wallet transaction description is required.");
        }

        return normalized.Length <= MaxDescriptionLength
            ? normalized
            : throw new DomainException($"Wallet transaction description must not exceed {MaxDescriptionLength} characters.");
    }
}
