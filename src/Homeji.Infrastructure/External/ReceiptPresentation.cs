using System.Globalization;
using Homeji.Application.DTOs.Emails;
using Homeji.Domain.Enums;

namespace Homeji.Infrastructure.External;

internal static class ReceiptPresentation
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public static string Amount(TransactionReceiptDto receipt) => receipt.Amount.ToString(receipt.Amount == decimal.Truncate(receipt.Amount) ? "N0" : "N2", Vietnamese) + " VND";
    public static string Time(TransactionReceiptDto receipt) => receipt.IssuedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm:ss", Vietnamese) + " (UTC+7)";
    public static string Id(TransactionReceiptDto receipt) => $"HJ-{receipt.Id:N}";
    public static string Status(ReceiptEvent value) => value switch
    {
        ReceiptEvent.WithdrawalRequested => "Đang chờ xử lý",
        ReceiptEvent.PurchaseRefunded or ReceiptEvent.WithdrawalRejected => "Đã hoàn tiền vào ví",
        ReceiptEvent.PurchasePaid or ReceiptEvent.PaymentSucceeded => "Đã thanh toán",
        ReceiptEvent.DepositSucceeded => "Đã nạp tiền",
        ReceiptEvent.WithdrawalCompleted => "Đã hoàn tất",
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };
    public static string Note(ReceiptEvent value) => value switch
    {
        ReceiptEvent.WithdrawalRequested => "Yêu cầu đang chờ xử lý. Biên nhận này không xác nhận tiền đã tới ngân hàng.",
        ReceiptEvent.PurchasePaid => "Đã thanh toán bằng số dư Homeji. Tiền đang được giữ đảm bảo đơn hàng; biên nhận không xác nhận hàng đã được giao hoặc người bán đã nhận tiền.",
        ReceiptEvent.PurchaseRefunded or ReceiptEvent.WithdrawalRejected => "Khoản tiền đã được hoàn vào số dư Homeji theo trạng thái giao dịch ghi nhận tại thời điểm phát hành.",
        _ => "Thông tin được ghi nhận theo trạng thái giao dịch tại Homeji ở thời điểm phát hành biên nhận.",
    };
}
