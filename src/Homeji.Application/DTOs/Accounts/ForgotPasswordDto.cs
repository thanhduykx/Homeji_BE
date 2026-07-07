namespace Homeji.Application.DTOs.Accounts;

public sealed record ForgotPasswordDto(
    string? Email,
    string? RedirectTo);
