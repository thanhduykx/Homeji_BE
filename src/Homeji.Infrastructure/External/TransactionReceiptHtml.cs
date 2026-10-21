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
        return $$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{WebUtility.HtmlEncode(title)}} | Homeji</title>
            <style>@media(max-width:560px){.receipt-outer{padding:12px 8px!important}.receipt-pad{padding:24px 20px!important}.receipt-column{display:block!important;width:100%!important;box-sizing:border-box!important;padding:0!important}.receipt-paper{margin-top:24px!important}.receipt-total{font-size:30px!important}.receipt-date{padding-top:18px!important}.receipt-title{font-size:23px!important} }</style></head>
            <body style="margin:0;background:#f0f4f3;color:#193737;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%">
            <div style="display:none;max-height:0;overflow:hidden;mso-hide:all">Biên nhận Homeji: {{WebUtility.HtmlEncode(title)}}.</div>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td class="receipt-outer" align="center" style="padding:32px 16px">
            <table role="presentation" width="640" cellspacing="0" cellpadding="0" style="width:100%;max-width:640px;background:#ffffff;border:1px solid #dbe5e2">
            <tr><td class="receipt-pad" style="padding:28px 36px 30px;background:#087f88">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td width="54" style="width:54px"><table role="presentation" cellspacing="0" cellpadding="0"><tr><td style="padding:5px;background:#ffffff;border-radius:8px"><img src="{{HomejiEmailLayout.LogoUrl}}" alt="Logo Homeji" width="36" height="36" style="display:block;border:0"></td></tr></table></td>
              <td><span style="font-size:28px;line-height:1.2;font-weight:700;color:#ffffff">Homeji</span><br><span style="font-size:11px;color:#d9f3ec">Một nơi để thuộc về</span></td><td align="right" style="font-size:10px;letter-spacing:1px;color:#d9f3ec">BIÊN NHẬN<br>ĐIỆN TỬ</td></tr></table>
            </td></tr>
            <tr><td style="height:4px;background:#84d9c5;font-size:0;line-height:0">&nbsp;</td></tr><tr><td style="height:4px;background:#ffffff;font-size:0;line-height:0">&nbsp;</td></tr><tr><td style="height:3px;background:#087f88;font-size:0;line-height:0">&nbsp;</td></tr>
            <tr><td class="receipt-pad" style="padding:28px 36px 30px">
              <p style="margin:0 0 9px;font-size:10px;letter-spacing:1.6px;color:#627976">GIAO DỊCH CỦA BẠN TẠI HOMEJI</p>
              <h1 class="receipt-title" style="margin:0 0 24px;font-size:25px;line-height:1.4;color:#087681">{{title}}</h1>
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr>
              <td class="receipt-column" width="48%" style="width:48%;vertical-align:top;padding-right:20px"><p style="margin:0 0 7px;font-size:12px;color:#506b66">Tổng tiền giao dịch</p><p class="receipt-total" style="margin:0;font-size:32px;line-height:1.25;font-weight:700;color:#087f88">{{amount}}</p></td>
              <td class="receipt-column receipt-date" style="vertical-align:top;padding-top:1px"><p style="margin:0 0 8px;font-size:12px;color:#506b66">Ngày · Giờ (UTC+7)</p><p style="margin:0;font-size:14px;font-weight:700;line-height:1.7;color:#193737">{{time}}</p></td></tr></table>
            </td></tr>
            <tr><td class="receipt-pad" style="padding:30px 36px;background:#f3f6f5">
              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="table-layout:fixed"><tr>
              <td class="receipt-column" width="39%" style="width:39%;padding-right:24px;vertical-align:top">
                <h2 style="margin:0 0 22px;font-size:12px;line-height:1.5;letter-spacing:.4px;color:#087681">CHI TIẾT GIAO DỊCH</h2>
                <p style="margin:0 0 5px;font-size:11px;color:#627976">Trạng thái</p><p style="margin:0 0 20px;font-size:13px;line-height:1.6;font-weight:700;color:#245b48">{{ReceiptPresentation.Status(receipt.Event)}}</p>
                <p style="margin:0 0 5px;font-size:11px;color:#627976">Mã giao dịch</p><p style="margin:0 0 20px;font-size:12px;line-height:1.7;word-break:break-all">{{reference}}</p>
                <p style="margin:0 0 5px;font-size:11px;color:#627976">Mã biên nhận</p><p style="margin:0;font-size:11px;line-height:1.7;word-break:break-all">{{ReceiptPresentation.Id(receipt)}}</p>
              </td>
              <td class="receipt-column" style="vertical-align:top">
                <table class="receipt-paper" role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border:1px solid #dbe5e2;background:#ffffff"><tr><td style="padding:22px 20px">
                  <h2 style="margin:0 0 5px;font-size:12px;letter-spacing:.4px;color:#087681">BIÊN NHẬN CỦA BẠN</h2>
                  <p style="margin:0 0 18px;font-size:10px;line-height:1.6;color:#627976">Được ghi nhận bởi Homeji</p>
                  <table width="100%" cellspacing="0" cellpadding="0" style="table-layout:fixed;border-collapse:collapse"><tr><th align="left" style="padding:11px 0;border-top:1px dashed #b8ccc5;border-bottom:1px dashed #b8ccc5;font-size:11px;font-weight:400;color:#627976">Nội dung giao dịch</th></tr>
                  <tr><td style="padding:16px 0 22px;font-size:12px;line-height:1.9;overflow-wrap:anywhere;word-break:break-word">{{description}}</td></tr></table>
                  <table width="100%" cellspacing="0" cellpadding="0" style="border-top:1px dashed #b8ccc5"><tr><th align="left" style="padding-top:18px;font-size:11px;line-height:1.6;color:#193737">TỔNG TIỀN</th><td align="right" style="padding-top:18px;padding-left:8px;font-size:16px;font-weight:700;white-space:nowrap;color:#087681">{{amount}}</td></tr></table>
                </td></tr></table>
              </td></tr></table>
            </td></tr>
            <tr><td class="receipt-pad" style="padding:26px 36px 30px">
              <p style="margin:0 0 22px;font-size:12px;line-height:1.9;color:#506b66">{{ReceiptPresentation.Note(receipt.Event)}}</p>
              <p style="margin:0 0 22px"><a href="{{WebUtility.HtmlEncode(HomejiEmailLayout.WalletHistoryUrl)}}" target="_blank" rel="noopener noreferrer" style="display:inline-block;padding:13px 21px;background:#087f88;border-radius:6px;text-decoration:none;color:#ffffff;font-size:13px;font-weight:700">Xem lịch sử giao dịch &nbsp; →</a></p>
              <p style="margin:0 0 22px;font-size:12px;line-height:1.8;color:#506b66">Nếu nút không mở được, dùng liên kết này trong trình duyệt và đăng nhập cùng tài khoản Homeji:<br><a href="{{WebUtility.HtmlEncode(HomejiEmailLayout.WalletHistoryUrl)}}" target="_blank" rel="noopener noreferrer" style="color:#087681;word-break:break-all">{{WebUtility.HtmlEncode(HomejiEmailLayout.WalletHistoryUrl)}}</a></p>
              <p style="margin:0;padding-top:20px;border-top:1px solid #e2ece9;font-size:11px;line-height:1.9;color:#627976">Biên nhận PDF và ảnh logo Homeji được đính kèm. Bạn có thể lưu PDF để in hoặc đối chiếu.<br>Đây là biên nhận giao dịch Homeji, <strong>không phải hóa đơn thuế/VAT</strong>.</p>
            </td></tr></table>
            <p style="margin:18px 0 0;font-size:11px;line-height:1.9;color:#627976">Homeji · Tìm phòng, kết nối và chia sẻ cuộc sống.<br><a href="{{HomejiEmailLayout.Website}}" style="color:#087681;text-decoration:none">homeji.onrender.com</a></p>
            </td></tr></table></body></html>
            """;
    }
}
