namespace Homeji.Application.DTOs.Admin;

public sealed record TerminateUserSessionRequestDto(string? Reason);

public sealed record TerminateUserSessionResultDto(
    Guid UserId,
    string DisplayName,
    string Reason,
    DateTimeOffset TerminatedAt);
