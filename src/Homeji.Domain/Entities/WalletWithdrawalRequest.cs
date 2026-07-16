using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class WalletWithdrawalRequest
{
    public const int MaxBankNameLength = 120;
    public const int MaxAccountNumberLength = 40;
    public const int MaxAccountHolderLength = 120;
    public const int MaxAdminNoteLength = 300;

    private WalletWithdrawalRequest()
    {
        BankName = null!;
        AccountNumber = null!;
        AccountHolder = null!;
    }

    public WalletWithdrawalRequest(
        Guid userId,
        decimal amount,
        string bankName,
        string accountNumber,
        string accountHolder,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("Withdrawal user id must not be empty.");
        }

        EnsurePositiveAmount(amount);
        Id = Guid.NewGuid();
        UserId = userId;
        Amount = amount;
        BankName = NormalizeRequired(bankName, MaxBankNameLength, "Bank name");
        AccountNumber = NormalizeRequired(accountNumber, MaxAccountNumberLength, "Bank account number");
        AccountHolder = NormalizeRequired(accountHolder, MaxAccountHolderLength, "Bank account holder").ToUpperInvariant();
        Status = WalletWithdrawalStatus.Pending;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }
    public string BankName { get; private set; }
    public string AccountNumber { get; private set; }
    public string AccountHolder { get; private set; }
    public WalletWithdrawalStatus Status { get; private set; }
    public string? AdminNote { get; private set; }
    public Guid? ProcessedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public void Complete(Guid adminId, string? note, DateTimeOffset processedAt)
    {
        EnsurePending(adminId);
        Status = WalletWithdrawalStatus.Completed;
        AdminNote = NormalizeOptional(note, MaxAdminNoteLength);
        ProcessedBy = adminId;
        ProcessedAt = processedAt;
    }

    public void Reject(Guid adminId, string? note, DateTimeOffset processedAt)
    {
        EnsurePending(adminId);
        Status = WalletWithdrawalStatus.Rejected;
        AdminNote = NormalizeOptional(note, MaxAdminNoteLength);
        ProcessedBy = adminId;
        ProcessedAt = processedAt;
    }

    private void EnsurePending(Guid adminId)
    {
        if (adminId == Guid.Empty)
        {
            throw new DomainException("Withdrawal processor id must not be empty.");
        }

        if (Status != WalletWithdrawalStatus.Pending)
        {
            throw new DomainException("Only pending withdrawal requests can be processed.");
        }
    }

    private static void EnsurePositiveAmount(decimal amount)
    {
        if (amount <= 0 || decimal.Truncate(amount) != amount)
        {
            throw new DomainException("Withdrawal amount must be a positive whole VND value.");
        }
    }

    private static string NormalizeRequired(string? value, int maxLength, string field)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new DomainException($"{field} is required.");
        }

        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"{field} must not exceed {maxLength} characters.");
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return null;
        return normalized.Length <= maxLength
            ? normalized
            : throw new DomainException($"Admin note must not exceed {maxLength} characters.");
    }
}
