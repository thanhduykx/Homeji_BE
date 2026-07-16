using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Marketplace;

public sealed class WalletWithdrawalRequestTests
{
    [Fact]
    public void Complete_WhenPending_RecordsProcessorAndTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var adminId = Guid.NewGuid();
        var request = new WalletWithdrawalRequest(
            Guid.NewGuid(), 100_000, "Vietcombank", "0123456789", "Nguyen Van A", now);

        request.Complete(adminId, "FT-123", now.AddMinutes(5));

        Assert.Equal(WalletWithdrawalStatus.Completed, request.Status);
        Assert.Equal(adminId, request.ProcessedBy);
        Assert.Equal("FT-123", request.AdminNote);
        Assert.Equal(now.AddMinutes(5), request.ProcessedAt);
    }

    [Fact]
    public void Reject_WhenAlreadyCompleted_ThrowsAndPreservesCompletedStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var request = new WalletWithdrawalRequest(
            Guid.NewGuid(), 100_000, "Vietcombank", "0123456789", "Nguyen Van A", now);
        request.Complete(Guid.NewGuid(), null, now.AddMinutes(1));

        Assert.Throws<DomainException>(() =>
            request.Reject(Guid.NewGuid(), "Không hợp lệ", now.AddMinutes(2)));
        Assert.Equal(WalletWithdrawalStatus.Completed, request.Status);
    }
}
