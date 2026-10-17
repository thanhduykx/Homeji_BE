using Homeji.Application.DTOs.Emails;

namespace Homeji.Application.IServices.Emails;

public interface ITransactionalEmailSender
{
    Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text,
        Guid idempotencyKey, CancellationToken cancellationToken, EmailAttachmentDto? attachment = null);
}
