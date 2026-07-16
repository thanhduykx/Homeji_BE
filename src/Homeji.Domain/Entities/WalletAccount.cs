using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class WalletAccount
{
    public const decimal MinimumTopUp = 100_000m;
    public const decimal MaximumTopUp = 5_000_000m;
    public const decimal MinimumWithdrawalReserve = 20_000m;

    private WalletAccount()
    {
    }

    private WalletAccount(Guid userId, DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Wallet user id must not be empty.");
        }

        UserId = userId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid UserId { get; private set; }
    public decimal Balance { get; private set; }
    public decimal TotalDeposited { get; private set; }
    public decimal TotalSpent { get; private set; }
    public decimal TotalEarned { get; private set; }
    public long Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public bool IsActivated => TotalDeposited >= MinimumTopUp;

    public static WalletAccount Create(Guid userId, DateTimeOffset createdAt) => new(userId, createdAt);

    public void CreditTopUp(decimal amount, DateTimeOffset updatedAt)
    {
        EnsurePositive(amount);
        Balance += amount;
        TotalDeposited += amount;
        Touch(updatedAt);
    }

    public void DebitPurchase(decimal amount, DateTimeOffset updatedAt)
    {
        EnsurePositive(amount);
        if (Balance < amount)
        {
            throw new DomainException("Wallet balance is insufficient for this purchase.");
        }

        Balance -= amount;
        TotalSpent += amount;
        Touch(updatedAt);
    }

    public void CreditRefund(decimal amount, DateTimeOffset updatedAt)
    {
        EnsurePositive(amount);
        Balance += amount;
        TotalSpent = Math.Max(0, TotalSpent - amount);
        Touch(updatedAt);
    }

    public void CreditSaleProceeds(decimal netAmount, DateTimeOffset updatedAt)
    {
        EnsurePositive(netAmount);
        Balance += netAmount;
        TotalEarned += netAmount;
        Touch(updatedAt);
    }

    public void DebitWithdrawal(decimal amount, DateTimeOffset updatedAt)
    {
        EnsurePositive(amount);
        if (Balance - amount < MinimumWithdrawalReserve)
        {
            throw new DomainException($"Wallet must retain at least {MinimumWithdrawalReserve:0} VND after withdrawal.");
        }

        Balance -= amount;
        Touch(updatedAt);
    }

    public void CreditWithdrawalRefund(decimal amount, DateTimeOffset updatedAt)
    {
        EnsurePositive(amount);
        Balance += amount;
        Touch(updatedAt);
    }

    private void Touch(DateTimeOffset updatedAt)
    {
        Version += 1;
        UpdatedAt = updatedAt;
    }

    private static void EnsurePositive(decimal amount)
    {
        if (amount <= 0 || decimal.Truncate(amount) != amount)
        {
            throw new DomainException("Wallet amount must be a positive whole VND value.");
        }
    }
}
