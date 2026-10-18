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
        var amount = ReceiptPresentation.Amount(receipt);
        var time = ReceiptPresentation.Time(receipt);
        var description = WebUtility.HtmlEncode(receipt.Description).Replace("\n", "<br>", StringComparison.Ordinal);
        var reference = WebUtility.HtmlEncode(receipt.ReferenceCode);
        var body = $$"""
            <p style="margin:0 0 8px;color:#087681;font-size:11px;letter-spacing:1.5px;font-weight:700">BIÊN NHẬN GIAO DỊCH</p>
            <h1 class="email-title" style="margin:0 0 18px;font-size:26px;line-height:1.4;color:#193737">{{title}}</h1>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef8f5;border:1px solid #cee6df;border-radius:12px"><tr><td style="padding:22px">
              <p style="margin:0 0 8px;color:#506b66;font-size:12px">SỐ TIỀN GIAO DỊCH</p>
              <p style="margin:0 0 12px;font-size:30px;font-weight:700;color:#096a72;line-height:1.3">{{amount}}</p>
              <span style="display:inline-block;padding:5px 10px;background:#d8eee6;color:#245b48;border-radius:20px;font-size:12px;font-weight:700">{{ReceiptPresentation.Status(receipt.Event)}}</span>
            </td></tr></table>
            <table width="100%" cellspacing="0" cellpadding="0" style="margin-top:22px;text-align:left;border-collapse:collapse;font-size:13px;line-height:1.8;table-layout:fixed">
              <tr><th style="width:32%;vertical-align:top;padding:12px 12px 12px 0;font-weight:400;color:#627976;border-bottom:1px solid #e2ece9">Mã biên nhận</th><td style="padding:12px 0;word-break:break-all;border-bottom:1px solid #e2ece9;color:#193737">{{ReceiptPresentation.Id(receipt)}}</td></tr>
              <tr><th style="vertical-align:top;padding:12px 12px 12px 0;font-weight:400;color:#627976;border-bottom:1px solid #e2ece9">Mã giao dịch</th><td style="padding:12px 0;word-break:break-all;border-bottom:1px solid #e2ece9;color:#193737">{{reference}}</td></tr>
              <tr><th style="vertical-align:top;padding:12px 12px 12px 0;font-weight:400;color:#627976;border-bottom:1px solid #e2ece9">Thời gian</th><td style="padding:12px 0;border-bottom:1px solid #e2ece9;color:#193737">{{time}}</td></tr>
            </table>
            <p style="margin:22px 0 8px;font-size:12px;color:#627976">NỘI DUNG GIAO DỊCH</p>
            <p style="margin:0;font-size:14px;line-height:1.8;color:#193737;overflow-wrap:anywhere;word-break:break-word">{{description}}</p>
            <p style="margin:20px 0;padding:14px 16px;background:#f6f8f7;border-left:3px solid #88b8aa;font-size:12px;line-height:1.8;color:#506b66">{{ReceiptPresentation.Note(receipt.Event)}}</p>
            <p style="margin:22px 0"><a href="{{HomejiEmailLayout.Website}}/receipts" style="display:inline-block;padding:13px 20px;background:#087f88;border-radius:8px;text-decoration:none;color:#ffffff;font-size:14px;font-weight:700">Xem lịch sử biên nhận</a></p>
            <p style="margin:0;font-size:12px;line-height:1.8;color:#627976">Bạn có thể lưu bản HTML hoặc PDF của biên nhận để đối chiếu.<br>Đây là biên nhận giao dịch Homeji, <strong>không phải hóa đơn thuế/VAT</strong>.</p>
            """;
        return HomejiEmailLayout.Wrap(title + " | Homeji", "Biên nhận Homeji: " + title + ".", body);
    }
}
