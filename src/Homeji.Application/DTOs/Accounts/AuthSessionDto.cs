namespace Homeji.Application.DTOs.Accounts;

public sealed record AuthSessionDto(
    string? AccessToken,
    string? TokenType,
    int? ExpiresIn,
    string? RefreshToken,
    Guid? UserId,
    string? Email,
    bool EmailConfirmationRequired,
    string Message);
