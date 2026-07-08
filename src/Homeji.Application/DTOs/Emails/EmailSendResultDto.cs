namespace Homeji.Application.DTOs.Emails;

public sealed record EmailSendResultDto(
    bool Sent,
    string Message);
