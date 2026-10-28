using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Payments;
using Homeji.Application.IServices.Emails;
using Homeji.Application.IServices.Payments;
using Homeji.Application.Mappers.Payments;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class PremiumWalletPaymentService(
    ApplicationDbContext db, UserContext userContext, IOptions<PremiumSubscriptionOptions> options,
    TimeProvider timeProvider, ITransactionReceiptService receipts) : IPremiumWalletPaymentService
{
    public async Task<PaymentDto> PurchaseAsync(string packageCode, WalletPremiumPaymentDto request, CancellationToken cancellationToken = default)
    {
        var userId = userContext.GetRequiredUserId();
        await userContext.GetRequiredProfileAsync(cancellationToken);
        if (request.RequestId == Guid.Empty) throw Invalid("requestId", "Mã yêu cầu thanh toán không hợp lệ.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // A transient commit error can replay this unit. Re-read state and resolve the
            // same request key before applying any new debit; do not reuse tracked writes.
            db.ChangeTracker.Clear();
            return await PurchaseCoreAsync(userId, packageCode, request, cancellationToken);
        });
    }

    private async Task<PaymentDto> PurchaseCoreAsync(Guid userId, string packageCode, WalletPremiumPaymentDto request, CancellationToken cancellationToken)
    {
        var orderCode = $"W{userId:N}{request.RequestId:N}";
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize retries and simultaneous purchases for this user across application instances.
        // Wallet Version still protects against concurrent marketplace/withdrawal operations.
        var lockKey = userId.ToString("N");
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", cancellationToken);
        var existing = await db.PaymentTransactions.AsNoTracking().SingleOrDefaultAsync(p => p.OrderCode == orderCode, cancellationToken);
        if (existing is not null)
        {
            if (!string.Equals(existing.PackageCode, packageCode.Trim(), StringComparison.OrdinalIgnoreCase) || existing.Amount != request.ExpectedAmount)
                throw Invalid("requestId", "Yêu cầu đã được dùng cho giao dịch khác. Vui lòng tải lại trang.");
            await transaction.CommitAsync(cancellationToken);
            return PaymentMapper.ToDto(existing);
        }
        var plan = options.Value.GetPlans().SingleOrDefault(p => string.Equals(p.Code, packageCode.Trim(), StringComparison.OrdinalIgnoreCase));
        if (plan is null) throw Invalid("packageCode", "Không tìm thấy gói Premium.");
        if (plan.Price <= 0 || decimal.Truncate(plan.Price) != plan.Price || plan.DurationDays <= 0 || string.IsNullOrWhiteSpace(plan.Name))
            throw new InvalidOperationException("Chưa cấu hình gói Premium.");
        if (request.ExpectedAmount != plan.Price)
            throw Invalid("expectedAmount", "Giá gói đã thay đổi. Vui lòng tải lại và xác nhận số tiền mới.");
        var wallet = await db.WalletAccounts.SingleOrDefaultAsync(w => w.UserId == userId, cancellationToken);
        if (wallet is null) throw Invalid("wallet", "Ví chưa có số dư. Vui lòng nạp tiền trước.");
        await db.Entry(wallet).ReloadAsync(cancellationToken);
        if (wallet.Balance < plan.Price) throw Invalid("wallet", "Số dư ví không đủ để thanh toán gói này.");
        var now = timeProvider.GetUtcNow();
        wallet.DebitPurchase(plan.Price, now);
        var description = $"{plan.Name} · {plan.DurationDays} ngày · Thanh toán bằng số dư ví Homeji";
        var payment = new PaymentTransaction(userId, PaymentMethod.Wallet, plan.Price, orderCode, description, now, PaymentPurpose.PremiumSubscription, plan.Code);
        payment.MarkPaid(null, "Thanh toán bằng số dư ví Homeji", null, now);
        db.PaymentTransactions.Add(payment);
        db.WalletTransactions.Add(new WalletTransaction(userId, WalletTransactionKind.SubscriptionPurchase,
            -plan.Price, wallet.Balance, payment.Id, description, now));
        var active = await db.UserSubscriptions.AsNoTracking().Where(s => s.UserId == userId && s.Tier == SubscriptionTier.Premium && s.Status == SubscriptionStatus.Active && s.ExpiresAt > now)
            .OrderByDescending(s => s.ExpiresAt).FirstOrDefaultAsync(cancellationToken);
        db.UserSubscriptions.Add(UserSubscription.CreatePremium(userId, plan.Code, plan.Name, payment.Id,
            active?.ExpiresAt ?? now, plan.DurationDays, now));
        await receipts.RecordPaymentAsync(payment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return PaymentMapper.ToDto(payment);
    }

    private static RequestValidationException Invalid(string field, string message) => new(new Dictionary<string, string[]> { [field] = [message] });
}
