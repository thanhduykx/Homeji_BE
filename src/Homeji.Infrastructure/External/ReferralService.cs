using System.Security.Cryptography;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.IServices.Promotions;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
namespace Homeji.Infrastructure.External;

public sealed class ReferralService(ApplicationDbContext db, UserContext userContext, TimeProvider clock) : IReferralService
{
    private const string CampaignId = "friends-v1";
    private static RequestValidationException Invalid(string message) => new(new Dictionary<string, string[]> { ["voucher"] = [message] });
    public async Task<ReferralSummary> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetRequiredUserId();
        var campaign = await db.ReferralCampaigns.AsNoTracking().SingleAsync(x => x.Id == CampaignId, cancellationToken);
        var code = await db.ReferralAccounts.Where(x => x.UserId == userId).Select(x => x.Code).SingleOrDefaultAsync(cancellationToken);
        var vouchers = await db.ReferralVouchers.AsNoTracking().Where(x => x.OwnerId == userId).OrderBy(x => x.ExpiresAt).ToListAsync(cancellationToken);
        return new(code, await db.ReferralClaims.CountAsync(x => x.InviterId == userId, cancellationToken),
            (int)Math.Max(0, (campaign.Budget - campaign.Reserved) / 100_000m),
            await db.ReferralClaims.AnyAsync(x => x.InviteeId == userId, cancellationToken),
            vouchers.Select(x => new VoucherDto(x.Id, x.Purpose, x.Rate, x.MaximumDiscount, x.MinimumSpend, x.ExpiresAt, x.ReferenceId.HasValue)).ToArray());
    }
    public async Task<ReferralSummary> CreateCodeAsync(CancellationToken cancellationToken = default)
    {
        var profile = await userContext.GetRequiredProfileAsync(cancellationToken);
        await RequireVerifiedAsync(profile.Id, false, cancellationToken);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(cancellationToken);
            if (!await db.ReferralAccounts.AnyAsync(x => x.UserId == profile.Id, cancellationToken))
            {
                db.ReferralAccounts.Add(new ReferralAccount(profile.Id, Convert.ToHexString(RandomNumberGenerator.GetBytes(12))));
                await db.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
        });
        return await GetAsync(cancellationToken);
    }
    public async Task<ReferralSummary> ClaimAsync(string code, CancellationToken cancellationToken = default)
    {
        var profile = await userContext.GetRequiredProfileAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(code) || code.Length != 24 || !code.All(Uri.IsHexDigit)) throw Invalid("Mã mời chưa đúng. Hãy kiểm tra lại link hoặc mã bạn nhận được.");
        var normalized = code.ToUpperInvariant();
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(cancellationToken);
            var inviter = await db.ReferralAccounts.AsNoTracking().SingleOrDefaultAsync(x => x.Code == normalized, cancellationToken)
                ?? throw Invalid("Không tìm thấy mã mời. Hãy nhờ bạn của bạn gửi lại link.");
            if (inviter.UserId == profile.Id) throw Invalid("Bạn không thể dùng mã mời của chính mình.");
            var previous = await db.ReferralClaims.AsNoTracking().SingleOrDefaultAsync(x => x.InviteeId == profile.Id, cancellationToken);
            if (previous is not null) {
                if (previous.InviterId != inviter.UserId) throw Invalid("Bạn đã nhận quà bằng một mã mời khác. Mỗi tài khoản chỉ nhận quà đăng ký một lần.");
                await transaction.CommitAsync(cancellationToken); return;
            }
            if (await db.ReferralClaims.AnyAsync(x => x.InviterId == profile.Id, cancellationToken))
                throw Invalid("Bạn đã nhận thưởng khi mời người khác. Không thể quay lại nhận quà đăng ký bằng mã mời khác.");
            await RequireVerifiedAsync(profile.Id, true, cancellationToken);
            await RequireVerifiedAsync(inviter.UserId, false, cancellationToken);
            if (await db.ReferralClaims.CountAsync(x => x.InviterId == inviter.UserId, cancellationToken) >= 5)
                throw Invalid("Mã mời này đã đạt giới hạn 5 lượt nhận quà trong đợt này.");
            var campaign = await db.ReferralCampaigns.SingleAsync(x => x.Id == CampaignId, cancellationToken);
            campaign.ReservePair();
            var now = clock.GetUtcNow();
            var claim = new ReferralClaim(inviter.UserId, profile.Id, now);
            db.ReferralClaims.Add(claim);
            foreach (var ownerId in new[] { inviter.UserId, profile.Id })
            {
                for (var i = 0; i < 3; i++) db.ReferralVouchers.Add(new ReferralVoucher(ownerId, claim.Id, "food", now));
                db.ReferralVouchers.Add(new ReferralVoucher(ownerId, claim.Id, "vip", now));
            }
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return await GetAsync(cancellationToken);
    }
    private Task<int> LockAsync(CancellationToken cancellationToken) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({CampaignId}, 0))", cancellationToken);
    private async Task RequireVerifiedAsync(Guid userId, bool newAccount, CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-7);
        var eligible = await db.Database.SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM auth.users WHERE id = {userId} AND email IS NOT NULL AND email_confirmed_at IS NOT NULL AND deleted_at IS NULL AND (NOT {newAccount} OR created_at >= {cutoff})) AS \"Value\"").SingleAsync(cancellationToken);
        if (!eligible) throw Invalid(newAccount ? "Quà dành cho tài khoản đăng ký trong 7 ngày và đã xác nhận email. Hãy xác nhận email rồi thử lại." : "Tài khoản cần xác nhận email trước khi tham gia mời bạn.");
    }
    private async Task<ReferralVoucher> FindAsync(Guid id, string purpose, CancellationToken cancellationToken)
    {
        var userId = userContext.GetRequiredUserId();
        var voucher = await db.ReferralVouchers.SingleOrDefaultAsync(x => x.Id == id && x.OwnerId == userId, cancellationToken)
            ?? throw Invalid("Không tìm thấy voucher trong tài khoản của bạn.");
        if (voucher.Purpose != purpose) throw Invalid("Voucher không áp dụng cho loại thanh toán này.");
        return voucher;
    }
    public async Task<decimal> QuoteAsync(Guid voucherId, string purpose, decimal amount, CancellationToken cancellationToken = default)
        => (await FindAsync(voucherId, purpose, cancellationToken)).Quote(amount, clock.GetUtcNow());
    public async Task<decimal> UseAsync(Guid voucherId, string purpose, decimal amount, Guid referenceId, CancellationToken cancellationToken = default)
    {
        var voucher = await FindAsync(voucherId, purpose, cancellationToken);
        voucher.Use(referenceId, amount, clock.GetUtcNow());
        // The caller commits voucher consumption with the payment/order and wallet debit.
        return voucher.Discount;
    }
    public async Task RestoreAsync(Guid referenceId, CancellationToken cancellationToken = default)
    {
        foreach (var voucher in await db.ReferralVouchers.Where(x => x.ReferenceId == referenceId).ToListAsync(cancellationToken)) voucher.Restore();
    }
}
