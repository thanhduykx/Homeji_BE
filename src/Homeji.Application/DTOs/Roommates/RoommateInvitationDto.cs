using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Roommates;

public sealed record RoommateInvitationDto(
    Guid Id,
    Guid RentalPostId,
    Guid SenderId,
    Guid ReceiverId,
    RoommateInvitationStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
