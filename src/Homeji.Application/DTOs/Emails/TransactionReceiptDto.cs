using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Emails;

public sealed record TransactionReceiptDto(Guid Id, Guid ReferenceId, ReceiptEvent Event,
    decimal Amount, string Currency, string ReferenceCode, string Description,
    DateTimeOffset IssuedAt, DateTimeOffset? EmailSentAt, int EmailAttempts, bool EmailFailed,
    bool EmailScheduled = true);

public sealed record ReceiptPageDto(IReadOnlyList<TransactionReceiptDto> Items, bool HasMore);
