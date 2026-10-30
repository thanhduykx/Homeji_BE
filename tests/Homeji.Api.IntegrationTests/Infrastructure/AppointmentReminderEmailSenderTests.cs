using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class AppointmentReminderEmailSenderTests
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Disabled_or_plaintext_SMTP_never_sends(bool enabled, bool tls)
    {
        var sender = new SmtpAccountEmailSender(Options.Create(new SmtpOptions
        {
            Enabled = enabled, EnableSsl = tls, Host = "smtp.example.com", FromEmail = "homeji@example.com",
        }), NullLogger<SmtpAccountEmailSender>.Instance);
        var result = await sender.SendAsync("renter@example.com", "Reminder", "<p>Reminder</p>", "Reminder", Guid.NewGuid(), CancellationToken.None);
        Assert.False(result.Sent);
        Assert.False(result.OutcomeUncertain);
    }
}
