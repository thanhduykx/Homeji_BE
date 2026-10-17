namespace Homeji.Infrastructure.External;

public sealed class EmailDeliveryOptions
{
    public string Provider { get; set; } = "Smtp";

    public bool RegistrationOtpEnabled { get; set; }

    public bool UsesBrevo => string.Equals(Provider, "Brevo", StringComparison.OrdinalIgnoreCase);
}
