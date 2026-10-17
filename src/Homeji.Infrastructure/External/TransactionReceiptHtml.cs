using System.Globalization;
using System.Net;
using Homeji.Application.DTOs.Emails;
using Homeji.Domain.Enums;

namespace Homeji.Infrastructure.External;

internal static class TransactionReceiptHtml
{
    public static string Title(ReceiptEvent receiptEvent) => receiptEvent switch
    {
        ReceiptEvent.DepositSucceeded => "Nạp tiền thành công",
        ReceiptEvent.PaymentSucceeded => "Thanh toán thành công",
        ReceiptEvent.PurchasePaid => "Biên nhận thanh toán mua hàng",
        ReceiptEvent.PurchaseRefunded => "Đã hoàn tiền mua hàng vào số dư Homeji",
        ReceiptEvent.WithdrawalRequested => "Đã nhận yêu cầu rút tiền",
        ReceiptEvent.WithdrawalCompleted => "Rút tiền hoàn tất",
        ReceiptEvent.WithdrawalRejected => "Yêu cầu rút tiền bị từ chối — đã hoàn tiền vào ví",
        _ => throw new ArgumentOutOfRangeException(nameof(receiptEvent)),
    };

    public static string Render(TransactionReceiptDto receipt)
    {
        var title = Title(receipt.Event);
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var amount = receipt.Amount.ToString(receipt.Amount == decimal.Truncate(receipt.Amount) ? "N0" : "N2", culture) + " VND";
        var time = receipt.IssuedAt.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm:ss", culture) + " (UTC+7)";
        var description = WebUtility.HtmlEncode(receipt.Description);
        var reference = WebUtility.HtmlEncode(receipt.ReferenceCode);
        return $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Biên nhận Homeji</title></head>
            <body style="margin:0;background:#f4f7f7;color:#162b2b;font-family:Arial,sans-serif;padding:24px">
            <main style="max-width:640px;margin:auto;background:white;padding:28px;border:1px solid #dbe6e5">
              <h1 style="font-size:24px;color:#087f88">Homeji</h1><h2>{{title}}</h2>
              <p>Biên nhận giao dịch <strong>HJ-{{receipt.Id:N}}</strong></p>
              <table style="width:100%;border-collapse:collapse;text-align:left"><tbody>
                <tr><th style="padding:10px 0">Mã giao dịch</th><td>{{reference}}</td></tr>
                <tr><th style="padding:10px 0">Số tiền</th><td>{{amount}}</td></tr>
                <tr><th style="padding:10px 0">Thời gian</th><td>{{time}}</td></tr>
                <tr><th style="padding:10px 0">Nội dung</th><td>{{description}}</td></tr>
              </tbody></table>
              <p>{{(receipt.Event == ReceiptEvent.WithdrawalRequested ? "Yêu cầu đang chờ xử lý; biên nhận này không xác nhận tiền đã tới ngân hàng." : "Thông tin được ghi nhận theo trạng thái giao dịch tại Homeji.")}}</p>
              <p style="font-size:12px;color:#526363">Đây là biên nhận giao dịch Homeji, không phải hóa đơn thuế/VAT. Xem lịch sử biên nhận trong tài khoản của bạn.</p>
            </main></body></html>
            """;
    }
}
