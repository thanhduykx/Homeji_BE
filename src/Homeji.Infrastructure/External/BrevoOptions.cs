namespace Homeji.Infrastructure.External;

public sealed class BrevoOptions
{
    public string ApiKey { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Homeji";

    public int TimeoutSeconds { get; set; } = 30;
}
