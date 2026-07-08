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
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            return new EmailSendResultDto(false, "SMTP registration confirmation email is disabled by configuration.");
        }

        ValidateOptions();

        var recipientName = NormalizeDisplayName(displayName);
        using var mailMessage = CreateRegistrationMessage(email, recipientName);
        using var smtpClient = CreateClient();

        try
        {
            await smtpClient.SendMailAsync(mailMessage, cancellationToken);
            return new EmailSendResultDto(true, "SMTP registration confirmation email was sent.");
        }
        catch (SmtpException exception)
        {
            FailedToSendRegistrationEmail(_logger, email, exception);
            return new EmailSendResultDto(false, "SMTP registration confirmation email could not be sent.");
        }
        catch (InvalidOperationException exception)
        {
            InvalidSmtpConfiguration(_logger, email, exception);
            return new EmailSendResultDto(false, "SMTP registration confirmation email could not be sent.");
        }
    }

    private MailMessage CreateRegistrationMessage(string email, string recipientName)
    {
        var from = new MailAddress(_options.FromEmail, _options.FromName);
        var to = new MailAddress(email, recipientName);
        var plainTextBody = BuildPlainTextBody(recipientName);
        var htmlBody = BuildHtmlBody(recipientName);

        return new MailMessage(from, to)
        {
            Subject = _options.RegistrationSubject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8,
            AlternateViews =
            {
                AlternateView.CreateAlternateViewFromString(
                    plainTextBody,
                    System.Text.Encoding.UTF8,
                    "text/plain"),
            },
        };
    }

    private SmtpClient CreateClient()
    {
        var timeoutMilliseconds = Math.Clamp(_options.TimeoutSeconds, 5, 120) * 1_000;
        var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = timeoutMilliseconds,
            UseDefaultCredentials = false,
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        return client;
    }

    private string BuildPlainTextBody(string recipientName)
    {
        return $"""
            Chào {recipientName},

            Homeji xác nhận tài khoản của bạn đã được đăng ký thành công.

            Nếu hệ thống yêu cầu xác thực email, hãy hoàn tất bước xác thực từ email Supabase Auth. Sau đó bạn có thể đăng nhập tại:
            {_options.LoginUrl}

            Nếu bạn không thực hiện đăng ký này, vui lòng bỏ qua email.

            Homeji
            """;
    }

    private string BuildHtmlBody(string recipientName)
    {
        var safeName = WebUtility.HtmlEncode(recipientName);
        var safeLoginUrl = WebUtility.HtmlEncode(_options.LoginUrl);

        return $$"""
            <!doctype html>
            <html lang="vi">
            <body style="font-family:Arial,sans-serif;line-height:1.6;color:#111827">
              <h2>Homeji xác nhận đăng ký tài khoản</h2>
              <p>Chào {{safeName}},</p>
              <p>Tài khoản Homeji của bạn đã được đăng ký thành công.</p>
              <p>Nếu hệ thống yêu cầu xác thực email, hãy hoàn tất bước xác thực từ email Supabase Auth. Sau đó bạn có thể đăng nhập tại:</p>
              <p><a href="{{safeLoginUrl}}">{{safeLoginUrl}}</a></p>
              <p>Nếu bạn không thực hiện đăng ký này, vui lòng bỏ qua email.</p>
              <p>Homeji</p>
            </body>
            </html>
            """;
    }

    private void ValidateOptions()
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
    }

    private static string NormalizeDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? "bạn"
            : displayName.Trim();
    }
}
