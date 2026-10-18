using System.Net;

namespace Homeji.Infrastructure.External;

internal sealed record AccountEmailContent(string Subject, string Html, string Text)
{
    public static AccountEmailContent Registration(string? displayName, string url) => Create(
        displayName, url, "Xác nhận đăng ký tài khoản Homeji",
        "Vui lòng xác nhận email để hoàn tất đăng ký tài khoản Homeji.", "Xác nhận email");

    public static AccountEmailContent PasswordReset(string url) => Create(
        null, url, "Đặt lại mật khẩu Homeji",
        "Bạn đã yêu cầu đặt lại mật khẩu Homeji. Mở liên kết bên dưới để chọn mật khẩu mới.", "Đặt lại mật khẩu");

    private static AccountEmailContent Create(string? displayName, string url, string subject, string message, string action)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("A valid account action URL must be provided.");
        }

        var name = string.IsNullOrWhiteSpace(displayName) ? "bạn" : displayName.Trim();
        var safeName = WebUtility.HtmlEncode(name);
        var safeUrl = WebUtility.HtmlEncode(url);
        var body = $$"""
            <p style="margin:0 0 8px;color:#087681;font-size:11px;font-weight:700;letter-spacing:1.5px">BẢO MẬT TÀI KHOẢN</p>
            <h1 class="email-title" style="margin:0 0 18px;font-size:26px;line-height:1.4;color:#193737">{{subject}}</h1>
            <p style="font-size:15px;line-height:1.8;color:#506b66">Chào {{safeName}},<br>{{message}}</p>
            <p style="margin:24px 0"><a href="{{safeUrl}}" style="display:inline-block;padding:13px 20px;background:#087f88;color:#fff;text-decoration:none;border-radius:8px;font-size:14px;font-weight:700">{{action}}</a></p>
            <p style="font-size:12px;line-height:1.8;color:#627976">Nếu nút không hoạt động, hãy mở liên kết sau:<br><span style="word-break:break-all">{{safeUrl}}</span></p>
            <p style="font-size:13px;line-height:1.8;color:#627976">Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
            """;
        var html = HomejiEmailLayout.Wrap(subject, message, body);
        var text = $"Chào {name},\n\n{message}\n{url}\n\nNếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.\n\nHomeji";
        return new AccountEmailContent(subject, html, text);
    }
}
