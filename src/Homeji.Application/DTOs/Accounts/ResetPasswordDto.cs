namespace Homeji.Application.DTOs.Accounts;

public sealed record ResetPasswordDto(
    string? AccessToken,
    string? NewPassword);
