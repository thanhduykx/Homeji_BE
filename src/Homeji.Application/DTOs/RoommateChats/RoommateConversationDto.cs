namespace Homeji.Application.DTOs.RoommateChats;

public sealed record RoommateConversationDto(
    Guid Id,
    Guid InvitationId,
    Guid RentalPostId,
    Guid OtherParticipantId,
    string OtherParticipantDisplayName,
    string? OtherParticipantAvatarPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
