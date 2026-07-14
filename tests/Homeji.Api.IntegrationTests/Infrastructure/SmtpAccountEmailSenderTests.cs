using Homeji.Infrastructure.External;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class SmtpAccountEmailSenderTests
{
    [Fact]
    public async Task SendRegistrationConfirmationAsync_WhenSmtpDisabled_DoesNotRequireSmtpSettings()
    {
        var sender = new SmtpAccountEmailSender(
            Options.Create(new SmtpOptions { Enabled = false }),
            NullLogger<SmtpAccountEmailSender>.Instance);

        var result = await sender.SendRegistrationConfirmationAsync(
            "user@example.com",
            "Homeji User",
            "https://project.supabase.co/auth/v1/verify?token=test&type=signup");

        Assert.False(result.Sent);
        Assert.Contains("disabled", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SendRegistrationConfirmationAsync_WhenSmtpConfigInvalid_ReturnsFailureInsteadOfThrowing()
    {
        var sender = new SmtpAccountEmailSender(
            Options.Create(new SmtpOptions
            {
                Enabled = true,
                Host = string.Empty,
                FromEmail = "not-an-email",
            }),
            NullLogger<SmtpAccountEmailSender>.Instance);

        var result = await sender.SendRegistrationConfirmationAsync(
            "user@example.com",
            "Homeji User",
            "https://project.supabase.co/auth/v1/verify?token=test&type=signup");

        Assert.False(result.Sent);
        Assert.Contains("could not be sent", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
