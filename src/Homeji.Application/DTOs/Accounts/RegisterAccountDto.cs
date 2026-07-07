namespace Homeji.Application.DTOs.Accounts;

public sealed record RegisterAccountDto(
    string? Email,
    string? Password,
    string? DisplayName,
    string? RedirectTo);
