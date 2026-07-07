using Homeji.Application.DTOs.Accounts;

namespace Homeji.Application.IServices.Accounts;

public interface IAccountService
{
    Task<AuthSessionDto> RegisterAsync(RegisterAccountDto request, CancellationToken cancellationToken = default);

    Task<AuthSessionDto> LoginAsync(LoginAccountDto request, CancellationToken cancellationToken = default);

    Task<AccountMessageDto> ForgotPasswordAsync(ForgotPasswordDto request, CancellationToken cancellationToken = default);

    Task<AccountMessageDto> ResetPasswordAsync(ResetPasswordDto request, CancellationToken cancellationToken = default);

    AuthUrlDto CreateGoogleLoginUrl(string? redirectTo);
}
