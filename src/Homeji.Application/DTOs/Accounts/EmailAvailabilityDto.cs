namespace Homeji.Application.DTOs.Accounts;

public sealed record EmailAvailabilityDto(
    string Email,
    bool Exists,
    bool Available);
