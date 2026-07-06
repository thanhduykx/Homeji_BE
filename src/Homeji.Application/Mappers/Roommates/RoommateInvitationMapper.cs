using Homeji.Application.DTOs.Roommates;
using Homeji.Domain.Entities;

namespace Homeji.Application.Mappers.Roommates;

public static class RoommateInvitationMapper
{
    public static RoommateInvitationDto ToDto(RoommateInvitation invitation)
    {
        return new RoommateInvitationDto(
            invitation.Id,
            invitation.RentalPostId,
            invitation.SenderId,
            invitation.ReceiverId,
            invitation.Status,
            invitation.CreatedAt,
            invitation.UpdatedAt);
    }
}
