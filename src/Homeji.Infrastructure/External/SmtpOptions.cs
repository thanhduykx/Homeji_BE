namespace Homeji.Infrastructure.External;

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public bool EnableSsl { get; set; } = true;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromEmail { get; set; } = string.Empty;

    public string FromName { get; set; } = "Homeji";

    public string RegistrationSubject { get; set; } = "Xác nhận đăng ký tài khoản Homeji";

    public int TimeoutSeconds { get; set; } = 30;
}
