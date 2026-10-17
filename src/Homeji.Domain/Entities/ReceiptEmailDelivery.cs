namespace Homeji.Domain.Entities;

public sealed class ReceiptEmailDelivery
{
    private ReceiptEmailDelivery() { }

    public ReceiptEmailDelivery(Guid receiptId, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        ReceiptId = receiptId;
        NextAttemptAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid ReceiptId { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string? LastErrorCode { get; private set; }
}
