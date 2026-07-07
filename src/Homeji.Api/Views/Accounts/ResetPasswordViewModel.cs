namespace Homeji.Api.Views.Accounts;

public sealed record ResetPasswordViewModel(
    string? AccessToken,
    string? NewPassword);
