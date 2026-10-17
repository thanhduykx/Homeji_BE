using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class TransactionReceipt
{
    private TransactionReceipt() { ReferenceCode = null!; Description = null!; }

    public TransactionReceipt(Guid userId, Guid referenceId, ReceiptEvent receiptEvent, decimal amount,
        string referenceCode, string description, DateTimeOffset issuedAt)
    {
        if (userId == Guid.Empty || referenceId == Guid.Empty || !Enum.IsDefined(receiptEvent)
            || amount <= 0)
            throw new DomainException("Dữ liệu biên nhận không hợp lệ.");
        if (string.IsNullOrWhiteSpace(referenceCode) || referenceCode.Length > 120
            || string.IsNullOrWhiteSpace(description) || description.Length > 500)
            throw new DomainException("Nội dung biên nhận không hợp lệ.");
        Id = Guid.NewGuid();
        UserId = userId;
        ReferenceId = referenceId;
        Event = receiptEvent;
        Amount = amount;
        ReferenceCode = referenceCode;
        Description = description;
        IssuedAt = issuedAt;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ReferenceId { get; private set; }
    public ReceiptEvent Event { get; private set; }
    public decimal Amount { get; private set; }
    public string ReferenceCode { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
}
