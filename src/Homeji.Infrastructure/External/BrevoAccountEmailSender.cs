using System.Net.Mail;
using System.Net.Http.Json;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class BrevoAccountEmailSender(
    HttpClient httpClient,
    IOptions<BrevoOptions> options,
    ILogger<BrevoAccountEmailSender> logger) : IAccountEmailSender
{
    private static readonly Action<ILogger, int, Exception?> SendFailed = LoggerMessage.Define<int>(
        LogLevel.Warning, new EventId(1003, nameof(SendFailed)),
        "Brevo account email request failed with HTTP status {StatusCode}.");

    public Task<EmailSendResultDto> SendRegistrationConfirmationAsync(
        string email, string? displayName, string confirmationUrl, CancellationToken cancellationToken = default) =>
        SendAsync(email, AccountEmailContent.Registration(displayName, confirmationUrl), cancellationToken);

    public Task<EmailSendResultDto> SendPasswordResetAsync(
        string email, string resetUrl, CancellationToken cancellationToken = default) =>
        SendAsync(email, AccountEmailContent.PasswordReset(resetUrl), cancellationToken);

    private async Task<EmailSendResultDto> SendAsync(
        string email, AccountEmailContent content, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey)
            || settings.ApiKey.Any(char.IsControl)
            || !MailAddress.TryCreate(settings.FromEmail, out var from)
            || !MailAddress.TryCreate(email, out var to))
        {
            return new EmailSendResultDto(false, "Cấu hình Brevo hoặc địa chỉ email không hợp lệ.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", settings.ApiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = from.Address, name = settings.FromName },
            to = new[] { new { email = to.Address } },
            subject = content.Subject,
            htmlContent = content.Html,
            textContent = content.Text,
        });

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 120)));
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return new EmailSendResultDto(true, "Brevo đã tiếp nhận email.");
            }

            // Never log provider response bodies: they may contain personal data or links.
            SendFailed(logger, (int)response.StatusCode, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SendFailed(logger, 0, null);
        }
        catch (HttpRequestException)
        {
            SendFailed(logger, 0, null);
        }

        // No automatic retries: a timeout can occur after the provider accepted the email.
        return new EmailSendResultDto(false, "Không gửi được email qua Brevo. Vui lòng thử lại sau.");
    }
}
