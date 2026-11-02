using System.Text.Json;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class RealEmailDeliveryTests
{
    [LiveEmailDeliveryFact]
    public async Task Brevo_accepts_one_explicit_QA_workbook_for_the_authorized_recipient()
    {
        var root = Environment.GetEnvironmentVariable("HOMEJI_EMAIL_LIVE_ROOT")!;
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root, "src/Homeji.Api/appsettings.Local.json")));
        var settings = document.RootElement.GetProperty("Email").GetProperty("Brevo");
        var options = new BrevoOptions { ApiKey = settings.GetProperty("ApiKey").GetString()!, FromEmail = settings.GetProperty("FromEmail").GetString()!, FromName = "Homeji QA" };
        var snapshot = AdminReportWorkbookTests.Sample() with { Accounts = [], Members = [] };
        var workbook = new AdminReportWorkbookWriter().Write(snapshot);
        using var http = new HttpClient();
        var sender = new BrevoAccountEmailSender(http, Options.Create(options), NullLogger<BrevoAccountEmailSender>.Instance);
        const string recipient = "duongduy12314@gmail.com";
        var requestId = Guid.NewGuid();
        var subject = "[Homeji QA] Nghiệm thu Excel " + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmm", System.Globalization.CultureInfo.InvariantCulture);
        const string text = "Email nghiệm thu Homeji. File Excel đính kèm chỉ chứa dữ liệu QA mẫu, không phải báo cáo production và không chứa danh sách tài khoản thật. Không có thanh toán nào được thực hiện.";
        var result = await sender.SendAsync(recipient, subject, "<p>" + text + "</p>", text, requestId, CancellationToken.None,
            [new EmailAttachmentDto("Homeji-QA-report.xlsx", Convert.ToBase64String(workbook))]);
        var evidence = Path.Combine(root, "output/verification/real-email-delivery.json");
        await File.WriteAllTextAsync(evidence, JsonSerializer.Serialize(new { subject, recipient, providerAccepted = result.Sent, inboxConfirmed = false, result.OutcomeUncertain, sampleDataOnly = true, requestId }));
        Assert.True(result.Sent, "Provider did not accept the QA email. Inspect the local evidence; do not automatically resend an uncertain delivery.");
    }
}

public sealed class LiveEmailDeliveryFactAttribute : FactAttribute
{
    public LiveEmailDeliveryFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOMEJI_EMAIL_LIVE_ROOT")))
            Skip = "Explicit opt-in required: sends one real QA workbook to the authorized Homeji report recipient.";
    }
}
