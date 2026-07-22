using System.Net;
using System.Net.Mail;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class SmtpAccountEmailSender : IAccountEmailSender
{
    private static readonly Action<ILogger, string, Exception?> FailedToSendRegistrationEmail =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1001, nameof(FailedToSendRegistrationEmail)),
            "Failed to send registration confirmation email to {Email}.");

    private static readonly Action<ILogger, string, Exception?> InvalidSmtpConfiguration =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(1002, nameof(InvalidSmtpConfiguration)),
            "SMTP registration confirmation email was not sent to {Email} because SMTP configuration is invalid.");

    private readonly ILogger<SmtpAccountEmailSender> _logger;
    private readonly SmtpOptions _options;

    public SmtpAccountEmailSender(
        IOptions<SmtpOptions> options,
        ILogger<SmtpAccountEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<EmailSendResultDto> SendRegistrationConfirmationAsync(
        string email,
        string? displayName,
        string confirmationUrl,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new EmailSendResultDto(false, "Email xác nhận đăng ký đang tắt theo cấu hình SMTP.");
        }

        try
        {
            ValidateOptions(confirmationUrl);
            var recipientName = NormalizeDisplayName(displayName);
            using var mailMessage = CreateRegistrationMessage(email, recipientName, confirmationUrl);
            using var smtpClient = CreateClient();

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            return new EmailSendResultDto(true, "Đã gửi email xác nhận đăng ký qua SMTP.");
        }
        catch (InvalidOperationException exception)
        {
            InvalidSmtpConfiguration(_logger, email, exception);
            return new EmailSendResultDto(false, "Không gửi được email xác nhận đăng ký qua SMTP.");
        }
        catch (FormatException exception)
        {
            InvalidSmtpConfiguration(_logger, email, exception);
            return new EmailSendResultDto(false, "Không gửi được email xác nhận đăng ký qua SMTP.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            FailedToSendRegistrationEmail(_logger, email, exception);
            return new EmailSendResultDto(false, "Không gửi được email xác nhận đăng ký qua SMTP.");
        }
    }

    private MailMessage CreateRegistrationMessage(string email, string recipientName, string confirmationUrl)
    {
        var from = new MailAddress(_options.FromEmail, _options.FromName);
        var to = new MailAddress(email, recipientName);

        return new MailMessage(from, to)
        {
            Subject = _options.RegistrationSubject,
            Body = BuildHtmlBody(recipientName, confirmationUrl),
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
            AlternateViews =
            {
                AlternateView.CreateAlternateViewFromString(
                    BuildPlainTextBody(recipientName, confirmationUrl),
                    System.Text.Encoding.UTF8,
                    "text/plain"),
            },
        };
    }

    private SmtpClient CreateClient()
    {
        var timeoutMilliseconds = Math.Clamp(_options.TimeoutSeconds, 5, 120) * 1_000;
        return new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = timeoutMilliseconds,
            UseDefaultCredentials = false,
            Credentials = string.IsNullOrWhiteSpace(_options.Username)
                ? null
                : new NetworkCredential(_options.Username, _options.Password),
        };
    }

    private static string BuildPlainTextBody(string recipientName, string confirmationUrl)
    {
        return $"""
            Chào {recipientName},

            Vui lòng xác nhận email để hoàn tất đăng ký tài khoản Homeji:
            {confirmationUrl}

            Nếu bạn không thực hiện đăng ký này, vui lòng bỏ qua email.

            Homeji
            """;
    }

    private static string BuildHtmlBody(string recipientName, string confirmationUrl)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeConfirmationUrl = WebUtility.HtmlEncode(confirmationUrl);

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;line-height:1.6;color:#111827">
              <h2>Xác nhận đăng ký tài khoản Homeji</h2>
              <p>Chào {{safeName}},</p>
              <p>Vui lòng xác nhận email để hoàn tất đăng ký tài khoản.</p>
              <p><a href="{{safeConfirmationUrl}}" style="display:inline-block;padding:12px 18px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px">Xác nhận email</a></p>
              <p>Nếu nút không hoạt động, hãy mở liên kết sau:</p>
              <p>{{safeConfirmationUrl}}</p>
              <p>Nếu bạn không thực hiện đăng ký này, vui lòng bỏ qua email.</p>
              <p>Homeji</p>
            </body>
            </html>
            """;
    }

    private void ValidateOptions(string confirmationUrl)
    {
        if (string.IsNullOrWhiteSpace(_options.Host))
        {
            throw new InvalidOperationException("SMTP host must be configured.");
        }

        if (_options.Port <= 0)
        {
            throw new InvalidOperationException("SMTP port must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(_options.FromEmail))
        {
            throw new InvalidOperationException("SMTP from email must be configured.");
        }

        _ = new MailAddress(_options.FromEmail);
        if (!Uri.TryCreate(confirmationUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("A valid confirmation URL must be provided.");
        }
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? "bạn" : displayName.Trim();
    }
}
