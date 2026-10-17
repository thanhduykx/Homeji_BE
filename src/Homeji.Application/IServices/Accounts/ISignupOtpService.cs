using Homeji.Application.DTOs.Accounts;

namespace Homeji.Application.IServices.Accounts;

public interface ISignupOtpService
{
    Task<AuthSessionDto> VerifySignupOtpAsync(SignupOtpDto request, CancellationToken cancellationToken = default);
    Task<AccountMessageDto> ResendSignupOtpAsync(ResendSignupOtpDto request, CancellationToken cancellationToken = default);
}
