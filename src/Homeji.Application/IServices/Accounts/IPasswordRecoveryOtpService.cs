using Homeji.Application.DTOs.Accounts;

namespace Homeji.Application.IServices.Accounts;

public interface IPasswordRecoveryOtpService
{
    Task<AccountMessageDto> RequestPasswordOtpAsync(RequestPasswordOtpDto request, CancellationToken cancellationToken = default);
    Task<AccountMessageDto> ResetPasswordWithOtpAsync(ResetPasswordOtpDto request, CancellationToken cancellationToken = default);
}
