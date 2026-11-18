namespace Homeji.Api.Controllers;

public sealed class PayPalSandboxOptions
{
    public const string SectionName = "Payments:PayPalSandbox";
    public bool Enabled { get; init; }
    public bool GooglePayEnabled { get; init; }
    public bool ApplePayEnabled { get; init; }
    public string FrontendOrigin { get; init; } = "http://localhost:5173";
}
