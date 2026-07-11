namespace Homeji.Infrastructure.External;

public sealed class SupaBaseAuthOptions
{
    public string ProjectUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string RegistrationRedirectUrl { get; set; } = string.Empty;

    public string PasswordResetRedirectUrl { get; set; } = string.Empty;

    public string GoogleRedirectUrl { get; set; } = string.Empty;
}
