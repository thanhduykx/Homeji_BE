using Homeji.Domain.Exceptions;
namespace Homeji.Domain.Entities;

public sealed class ReferralCampaign
{
    public string Id { get; private set; } = "friends-v1";
    public decimal Budget { get; private set; } = 1_000_000m;
    public decimal Reserved { get; private set; }
    public long Version { get; private set; }
    public void ReservePair()
    {
        if (Reserved + 100_000m > Budget) throw new DomainException("Quà của đợt này đã được nhận hết. Bạn vẫn có thể chia sẻ Homeji; hiện chưa có đợt thưởng mới.");
        Reserved += 100_000m; Version++;
    }
}

public sealed class ReferralAccount
{
    private ReferralAccount() { }
    public ReferralAccount(Guid userId, string code) { UserId = userId; Code = code; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; } = null!;
}

public sealed class ReferralClaim
{
    private ReferralClaim() { }
    public ReferralClaim(Guid inviterId, Guid inviteeId, DateTimeOffset now)
    { Id = Guid.NewGuid(); InviterId = inviterId; InviteeId = inviteeId; CreatedAt = now; }
    public Guid Id { get; private set; }
    public Guid InviterId { get; private set; }
    public Guid InviteeId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

public sealed class ReferralVoucher
{
    private ReferralVoucher() { }
    public ReferralVoucher(Guid ownerId, Guid claimId, string purpose, DateTimeOffset now)
    { Id = Guid.NewGuid(); OwnerId = ownerId; ClaimId = claimId; Purpose = purpose; ExpiresAt = now.AddDays(30); }
    public Guid Id { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid ClaimId { get; private set; }
    public string Purpose { get; private set; } = null!;
    public DateTimeOffset ExpiresAt { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public decimal Discount { get; private set; }
    public long Version { get; private set; }
    public decimal MaximumDiscount => Purpose == "food" ? 10_000m : 20_000m;
    public decimal MinimumSpend => Purpose == "food" ? 100_000m : 50_000m;
    public decimal Rate => Purpose == "food" ? .05m : .20m;
    public decimal Quote(decimal amount, DateTimeOffset now)
    {
        if (ReferenceId.HasValue) throw new DomainException("Voucher này đã được dùng. Hãy chọn voucher khác.");
        if (now >= ExpiresAt) throw new DomainException("Voucher đã hết hạn. Hãy chọn voucher khác.");
        if (amount < MinimumSpend) throw new DomainException($"Voucher cần đơn từ {MinimumSpend:N0}đ trước giảm giá.");
        return Math.Min(MaximumDiscount, decimal.Floor(amount * Rate));
    }
    public void Use(Guid referenceId, decimal amount, DateTimeOffset now)
    { Discount = Quote(amount, now); ReferenceId = referenceId; Version++; }
    public void Restore() { ReferenceId = null; Discount = 0; Version++; }
}
