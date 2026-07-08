using Homeji.Application.DTOs.Emails;

namespace Homeji.Application.IServices.Emails;

public interface IAccountEmailSender
{
    Task<EmailSendResultDto> SendRegistrationConfirmationAsync(
        string email,
        string? displayName,
        CancellationToken cancellationToken = default);
}
