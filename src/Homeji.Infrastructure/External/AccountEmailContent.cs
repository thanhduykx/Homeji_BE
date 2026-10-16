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
        var html = $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;line-height:1.6;color:#111827">
              <h2>{{subject}}</h2>
              <p>Chào {{safeName}},</p>
              <p>{{message}}</p>
              <p><a href="{{safeUrl}}" style="display:inline-block;padding:12px 18px;background:#0f766e;color:#fff;text-decoration:none;border-radius:6px">{{action}}</a></p>
              <p>Nếu nút không hoạt động, hãy mở liên kết sau:</p>
              <p>{{safeUrl}}</p>
              <p>Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.</p>
              <p>Homeji</p>
            </body>
            </html>
            """;
        var text = $"Chào {name},\n\n{message}\n{url}\n\nNếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.\n\nHomeji";
        return new AccountEmailContent(subject, html, text);
    }
}
