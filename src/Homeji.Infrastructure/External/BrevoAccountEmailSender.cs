using System.Net.Mail;
using System.Net.Http.Json;
using System.Text.Json;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.External;

public sealed class BrevoAccountEmailSender(
    HttpClient httpClient,
    IOptions<BrevoOptions> options,
    ILogger<BrevoAccountEmailSender> logger) : IAccountEmailSender, ITransactionalEmailSender
{
    private static readonly JsonSerializerOptions RequestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
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
        string email, AccountEmailContent content, CancellationToken cancellationToken, Guid? idempotencyKey = null,
        IReadOnlyList<EmailAttachmentDto>? attachments = null)
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
            attachment = attachments?.Select(item => new { name = item.Name, content = item.Content }).ToArray(),
            headers = idempotencyKey is null ? null : new Dictionary<string, string> { ["idempotencyKey"] = idempotencyKey.Value.ToString("D") },
        }, options: RequestJsonOptions);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 120)));
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode)
            {
                return new EmailSendResultDto(true, "Brevo đã tiếp nhận email.");
            }

            if (idempotencyKey is not null && response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                try
                {
                    using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
                    if (error.RootElement.TryGetProperty("code", out var code)
                        && code.GetString() == "duplicate_parameter"
                        && error.RootElement.TryGetProperty("message", out var message)
                        && message.GetString()?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) == true)
                        return new EmailSendResultDto(true, "Brevo đã tiếp nhận yêu cầu email này trước đó.");
                }
                catch (JsonException) { }
            }

            // Never log provider response bodies: they may contain personal data or links.
            SendFailed(logger, (int)response.StatusCode, null);
            return new EmailSendResultDto(false, "Brevo chưa xác nhận tiếp nhận email.", (int)response.StatusCode >= 500);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            SendFailed(logger, 0, null);
            return new EmailSendResultDto(false, "Kết quả gửi email chưa xác định.", true);
        }
        catch (HttpRequestException)
        {
            SendFailed(logger, 0, null);
            return new EmailSendResultDto(false, "Kết quả gửi email chưa xác định.", true);
        }

    }

    public Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text,
        Guid idempotencyKey, CancellationToken cancellationToken, IReadOnlyList<EmailAttachmentDto>? attachments = null) =>
        SendAsync(email, new AccountEmailContent(subject, html, text), cancellationToken, idempotencyKey, attachments);
}
