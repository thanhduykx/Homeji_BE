namespace Homeji.Application.DTOs.Accounts;

public sealed record LoginAccountDto(
    string? Email,
    string? Password);
