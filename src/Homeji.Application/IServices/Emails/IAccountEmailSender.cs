using Homeji.Application.DTOs.Emails;

namespace Homeji.Application.IServices.Emails;

public interface IAccountEmailSender
{
    Task<EmailSendResultDto> SendPasswordResetAsync(
        string email,
        string resetUrl,
        CancellationToken cancellationToken = default);

    Task<EmailSendResultDto> SendRegistrationConfirmationAsync(
        string email,
        string? displayName,
        string confirmationUrl,
        CancellationToken cancellationToken = default);
}
