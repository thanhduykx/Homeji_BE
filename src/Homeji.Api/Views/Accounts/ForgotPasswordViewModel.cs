namespace Homeji.Api.Views.Accounts;

public sealed record ForgotPasswordViewModel(
    string? Email,
    string? RedirectTo);
