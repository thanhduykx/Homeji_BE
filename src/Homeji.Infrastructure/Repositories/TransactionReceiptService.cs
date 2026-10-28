using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class TransactionReceiptService(ApplicationDbContext db, UserContext userContext) : ITransactionReceiptService, IOrderReceiptWriter
{
    public Task RecordOrderAsync(MarketplaceOrder order, string itemTitle, CancellationToken cancellationToken)
    {
        var refunded = order.RefundedAt.HasValue;
        var title = itemTitle.Length > 220 ? itemTitle[..220] : itemTitle;
        var description = refunded
            ? $"Hoàn tiền vào số dư Homeji · {title} · Số lượng {order.Quantity}"
            : $"{title} · Số lượng {order.Quantity} · Đơn giá {order.UnitPrice:0.##} VND · Thanh toán bằng số dư Homeji; tiền đang được giữ đảm bảo đơn hàng.";
        return RecordAsync(new TransactionReceipt(order.BuyerId, order.Id,
            refunded ? ReceiptEvent.PurchaseRefunded : ReceiptEvent.PurchasePaid,
            order.BuyerPaidAmount, order.Id.ToString("N"), description + (order.DiscountAmount > 0 ? $" Voucher giảm {order.DiscountAmount:0}đ." : ""),
            order.RefundedAt ?? order.CreatedAt), cancellationToken);
    }

    public Task RecordPaymentAsync(PaymentTransaction payment, CancellationToken cancellationToken)
    {
        if (payment.Status != PaymentStatus.Paid) throw new InvalidOperationException("Only paid payments have success receipts.");
        return RecordAsync(new TransactionReceipt(payment.UserId, payment.Id,
            payment.Purpose == PaymentPurpose.WalletTopUp ? ReceiptEvent.DepositSucceeded : ReceiptEvent.PaymentSucceeded,
            payment.Amount, payment.OrderCode, payment.Description, payment.PaidAt ?? payment.UpdatedAt), cancellationToken);
    }

    public Task RecordWithdrawalAsync(WalletWithdrawalRequest withdrawal, CancellationToken cancellationToken)
    {
        var receiptEvent = withdrawal.Status switch
        {
            WalletWithdrawalStatus.Pending => ReceiptEvent.WithdrawalRequested,
            WalletWithdrawalStatus.Completed => ReceiptEvent.WithdrawalCompleted,
            WalletWithdrawalStatus.Rejected => ReceiptEvent.WithdrawalRejected,
            _ => throw new InvalidOperationException("Unknown withdrawal status."),
        };
        // Account numbers and internal review notes never enter emails or receipt history.
        var details = $"Ngân hàng {withdrawal.BankName} ••••{withdrawal.AccountNumber[^Math.Min(4, withdrawal.AccountNumber.Length)..]}";
        return RecordAsync(new TransactionReceipt(withdrawal.UserId, withdrawal.Id, receiptEvent,
            withdrawal.Amount, withdrawal.Id.ToString("N"), details,
            withdrawal.ProcessedAt ?? withdrawal.CreatedAt), cancellationToken);
    }

    private async Task RecordAsync(TransactionReceipt receipt, CancellationToken cancellationToken)
    {
        if (db.TransactionReceipts.Local.Any(x => x.ReferenceId == receipt.ReferenceId && x.Event == receipt.Event)
            || await db.TransactionReceipts.AnyAsync(x => x.ReferenceId == receipt.ReferenceId && x.Event == receipt.Event, cancellationToken)) return;
        db.TransactionReceipts.Add(receipt);
        db.ReceiptEmailDeliveries.Add(new ReceiptEmailDelivery(receipt.Id, receipt.IssuedAt));
    }

    public async Task<ReceiptPageDto> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        if (page is < 1 or > 100_000 || pageSize is < 1 or > 100)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["page"] = ["Trang phải từ 1; kích thước trang từ 1 đến 100."] });
        var items = await LoadAsync(QueryMine().OrderByDescending(x => x.IssuedAt).ThenByDescending(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize + 1), cancellationToken);
        return new ReceiptPageDto(items.Take(pageSize).ToArray(), items.Length > pageSize);
    }

    public async Task<TransactionReceiptDto> GetMineByIdAsync(Guid id, CancellationToken cancellationToken) =>
        (await LoadAsync(QueryMine().Where(x => x.Id == id), cancellationToken)).SingleOrDefault()
        ?? throw new NotFoundException(nameof(TransactionReceipt), id);

    public async Task<string> GetMyHtmlAsync(Guid id, CancellationToken cancellationToken) =>
        TransactionReceiptHtml.Render(await GetMineByIdAsync(id, cancellationToken));

    private IQueryable<TransactionReceipt> QueryMine()
    {
        var userId = userContext.GetRequiredUserId();
        return db.TransactionReceipts.AsNoTracking().Where(x => x.UserId == userId);
    }

    private async Task<TransactionReceiptDto[]> LoadAsync(IQueryable<TransactionReceipt> query, CancellationToken cancellationToken)
    {
        var rows = await (from receipt in query
               join job in db.ReceiptEmailDeliveries.AsNoTracking() on receipt.Id equals job.ReceiptId into deliveries
               from delivery in deliveries.DefaultIfEmpty()
               select new { Receipt = receipt, Delivery = delivery }).ToArrayAsync(cancellationToken);
        return rows.OrderByDescending(x => x.Receipt.IssuedAt).ThenByDescending(x => x.Receipt.Id).Select(x =>
            new TransactionReceiptDto(x.Receipt.Id, x.Receipt.ReferenceId, x.Receipt.Event, x.Receipt.Amount, "VND",
                x.Receipt.ReferenceCode, x.Receipt.Description, x.Receipt.IssuedAt,
                x.Delivery?.SentAt, x.Delivery?.Attempts ?? 0,
                x.Delivery is { SentAt: null, Attempts: >= 8 }, x.Delivery != null)).ToArray();
    }
}
