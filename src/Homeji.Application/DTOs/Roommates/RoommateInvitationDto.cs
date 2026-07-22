using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Roommates;

public sealed record RoommateInvitationDto(
    Guid Id,
    Guid RentalPostId,
    string RentalPostTitle,
    Guid SenderId,
    Guid ReceiverId,
    RoommateInvitationStatus Status,
    Guid? ConversationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
