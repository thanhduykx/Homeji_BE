using Homeji.Application.DTOs.Roommates;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Roommates;

public static class RoommateInvitationMapper
{
    public static RoommateInvitationDto ToDto(
        RoommateInvitation invitation,
        string rentalPostTitle,
        Guid? conversationId = null)
    {
        return new RoommateInvitationDto(
            invitation.Id,
            invitation.RentalPostId,
            rentalPostTitle,
            invitation.SenderId,
            invitation.ReceiverId,
            invitation.Status,
            conversationId,
            invitation.CreatedAt,
            invitation.UpdatedAt);
    }
}
