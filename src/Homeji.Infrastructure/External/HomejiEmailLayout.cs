using System.Net;

namespace Homeji.Infrastructure.External;

internal static class HomejiEmailLayout
{
    public const string Website = "https://homeji.onrender.com";
    public const string LogoUrl = Website + "/brand/homeji-logo2.png";

    // Only trusted template markup enters content. Callers encode every user-controlled value.
    public static string Wrap(string title, string preheader, string content) => $$"""
        <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{WebUtility.HtmlEncode(title)}}</title>
        <style>@media(max-width:480px){.email-outer{padding:12px!important}.email-inner{padding:24px 18px!important}.email-title{font-size:23px!important} }</style></head>
        <body style="margin:0;padding:0;background:#f1f6f5;color:#193737;font-family:Arial,Helvetica,sans-serif;-webkit-text-size-adjust:100%">
        <div style="display:none;max-height:0;overflow:hidden;mso-hide:all">{{WebUtility.HtmlEncode(preheader)}}</div>
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f1f6f5"><tr><td class="email-outer" align="center" style="padding:32px 16px">
        <table role="presentation" width="600" cellspacing="0" cellpadding="0" style="width:100%;max-width:600px;background:#ffffff;border:1px solid #dbe8e5;border-top:5px solid #087f88;border-radius:16px">
        <tr><td class="email-inner" style="padding:30px 32px">
        <table role="presentation" cellspacing="0" cellpadding="0"><tr><td style="padding-right:12px"><img src="{{LogoUrl}}" width="48" height="48" alt="Logo Homeji" style="display:block;border:0"></td>
        <td><span style="font-size:28px;font-weight:700;color:#176f72;line-height:1.2">Homeji</span><br><span style="font-size:12px;color:#627976">Một nơi để thuộc về</span></td></tr></table>
        <div style="height:1px;background:#e2ece9;margin:24px 0"></div>
        {{content}}
        <div style="height:1px;background:#e2ece9;margin:26px 0 18px"></div>
        <p style="margin:0;font-size:12px;line-height:1.8;color:#627976">Email tự động từ Homeji. Vui lòng kiểm tra kỹ thông tin trước khi thực hiện yêu cầu.<br>
        <a href="{{Website}}" style="color:#087681;text-decoration:underline">homeji.onrender.com</a></p>
        </td></tr></table>
        <p style="margin:18px 0 0;font-size:11px;color:#6c817d;line-height:1.6">Homeji · Tìm phòng, kết nối và chia sẻ cuộc sống.</p>
        </td></tr></table></body></html>
        """;

    public static string Otp(string code, bool recovery)
    {
        if (code.Length is < 6 or > 10 || code.Any(c => c is < '0' or > '9'))
            throw new ArgumentException("OTP must contain 6 to 10 digits.", nameof(code));
        var title = recovery ? "Đặt lại mật khẩu" : "Xác thực đăng ký";
        var purpose = recovery ? "đặt lại mật khẩu Homeji" : "hoàn tất đăng ký tài khoản Homeji";
        var body = $$"""
            <p style="margin:0 0 8px;color:#087681;font-size:11px;font-weight:700;letter-spacing:1.5px">BẢO MẬT TÀI KHOẢN</p>
            <h1 class="email-title" style="margin:0 0 14px;font-size:27px;line-height:1.3;color:#193737">{{title}}</h1>
            <p style="margin:0 0 22px;font-size:15px;line-height:1.8;color:#506b66">Chào bạn,<br>Nhập mã dưới đây trên website để {{purpose}}.</p>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#eef8f5;border:1px solid #cee6df;border-radius:12px"><tr><td align="center" style="padding:22px 12px">
            <p style="margin:0 0 12px;font-size:12px;color:#506b66">MÃ XÁC THỰC CỦA BẠN</p>
            <p style="margin:0;font-family:'Courier New',monospace;font-size:28px;line-height:1.5;font-weight:700;letter-spacing:3px;color:#096a72">{{code}}</p>
            <p style="margin:12px 0 0;font-size:12px;color:#506b66">Có hiệu lực tối đa <strong>10 phút</strong> · Chỉ sử dụng một lần</p>
            </td></tr></table>
            <p style="margin:22px 0 0;font-size:14px;line-height:1.8;color:#506b66"><strong style="color:#193737">Giữ mã này riêng tư.</strong> Homeji không yêu cầu bạn chia sẻ mã OTP qua cuộc gọi hay tin nhắn.</p>
            <p style="margin:12px 0 0;font-size:13px;line-height:1.8;color:#627976">Nếu bạn không thực hiện yêu cầu này, hãy bỏ qua email. Không chuyển tiếp mã cho người khác.</p>
            """;
        return Wrap(title + " Homeji", "Mã xác thực Homeji có hiệu lực tối đa 10 phút.", body);
    }
}
