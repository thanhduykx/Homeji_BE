using Homeji.Application.DTOs.Roommates;

namespace Homeji.Application.IServices.Roommates;

public interface IRoommateInvitationService
{
    Task<RoommateInvitationDto> CreateAsync(
        Guid postId,
        CreateRoommateInvitationDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoommateInvitationDto>> GetMineAsync(CancellationToken cancellationToken = default);

    Task<RoommateInvitationDto> AcceptAsync(Guid invitationId, CancellationToken cancellationToken = default);

    Task<RoommateInvitationDto> RejectAsync(Guid invitationId, CancellationToken cancellationToken = default);

    Task<RoommateInvitationDto> CancelAsync(Guid invitationId, CancellationToken cancellationToken = default);
}
