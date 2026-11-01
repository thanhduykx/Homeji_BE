namespace Homeji.Domain.Entities;

public sealed class AdminReportExport
{
    private AdminReportExport() { }
    public AdminReportExport(Guid requestedById, Guid requestId, int periodDays, string recipient, byte[] content, DateTimeOffset createdAt)
    {
        if (requestedById == Guid.Empty || requestId == Guid.Empty || periodDays is < 7 or > 90
            || string.IsNullOrWhiteSpace(recipient) || recipient.Length > 254 || content.Length is < 1 or > 5 * 1024 * 1024)
            throw new Homeji.Domain.Exceptions.DomainException("Báo cáo không hợp lệ hoặc vượt quá giới hạn 5 MB.");
        Id = Guid.NewGuid(); RequestedById = requestedById; RequestId = requestId; PeriodDays = periodDays;
        Recipient = recipient; Content = content; CreatedAt = createdAt; NextAttemptAt = createdAt;
        FileName = $"Homeji-bao-cao-{createdAt.ToOffset(TimeSpan.FromHours(7)):yyyyMMdd-HHmmss}-{Id.ToString("N")[..8]}.xlsx";
    }
    public Guid Id { get; private set; }
    public Guid RequestedById { get; private set; }
    public Guid RequestId { get; private set; }
    public int PeriodDays { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public byte[] Content { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public string EmailStatus { get; private set; } = "queued";
    public DateTimeOffset? EmailAcceptedAt { get; private set; }
    public int Attempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public Guid? LeaseId { get; private set; }
    public DateTimeOffset? LeaseUntil { get; private set; }
    public string? LastErrorCode { get; private set; }
}
