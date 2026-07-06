using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Roommates;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IServices.Roommates;
using Homeji.Application.Mappers.Roommates;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Roommates;

public sealed class RoommateInvitationService : IRoommateInvitationService
{
    private readonly UserContext _userContext;
    private readonly IRoommateInvitationRepository _invitations;
    private readonly ISavedPostRepository _savedPosts;
    private readonly IRentalPostRepository _posts;
    private readonly INotificationRepository _notifications;
    private readonly TimeProvider _timeProvider;

    public RoommateInvitationService(
        UserContext userContext,
        IRoommateInvitationRepository invitations,
        ISavedPostRepository savedPosts,
        IRentalPostRepository posts,
        INotificationRepository notifications,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _invitations = invitations;
        _savedPosts = savedPosts;
        _posts = posts;
        _notifications = notifications;
        _timeProvider = timeProvider;
    }

    public async Task<RoommateInvitationDto> CreateAsync(
        Guid postId,
        CreateRoommateInvitationDto request,
        CancellationToken cancellationToken = default)
    {
        var senderId = _userContext.GetRequiredUserId();
        if (senderId == request.ReceiverId)
        {
            throw new ForbiddenAccessException("Cannot invite yourself.");
        }

        var post = await _posts.GetByIdAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        if (post.Status != RentalPostStatus.Active)
        {
            throw new NotFoundException(nameof(RentalPost), postId);
        }

        if (!await _savedPosts.ExistsAsync(senderId, postId, cancellationToken)
            || !await _savedPosts.ExistsAsync(request.ReceiverId, postId, cancellationToken))
        {
            throw new ForbiddenAccessException("Both users must save this rental post before creating a roommate invitation.");
        }

        if (await _invitations.HasPendingAsync(postId, senderId, request.ReceiverId, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["receiverId"] = ["A pending invitation already exists."],
            });
        }

        var invitation = new RoommateInvitation(postId, senderId, request.ReceiverId, _timeProvider.GetUtcNow());
        await _invitations.AddAsync(invitation, cancellationToken);
        await _notifications.AddAsync(new Notification(
            request.ReceiverId,
            NotificationType.RoommateInvitationReceived,
            "Lời mời ở ghép mới",
            "Có người cũng quan tâm phòng này muốn ở ghép với bạn.",
            invitation.Id,
            _timeProvider.GetUtcNow()), cancellationToken);
        await _invitations.SaveChangesAsync(cancellationToken);
        return RoommateInvitationMapper.ToDto(invitation);
    }

    public async Task<IReadOnlyList<RoommateInvitationDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var invitations = await _invitations.GetForUserAsync(userId, cancellationToken);
        return invitations.Select(RoommateInvitationMapper.ToDto).ToArray();
    }

    public Task<RoommateInvitationDto> AcceptAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(invitationId, accept: true, cancel: false, cancellationToken);
    }

    public Task<RoommateInvitationDto> RejectAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(invitationId, accept: false, cancel: false, cancellationToken);
    }

    public Task<RoommateInvitationDto> CancelAsync(Guid invitationId, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(invitationId, accept: false, cancel: true, cancellationToken);
    }

    private async Task<RoommateInvitationDto> UpdateAsync(
        Guid invitationId,
        bool accept,
        bool cancel,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var invitation = await _invitations.GetByIdAsync(invitationId, cancellationToken)
            ?? throw new NotFoundException(nameof(RoommateInvitation), invitationId);

        if (cancel)
        {
            UserContext.EnsureOwner(userId, invitation.SenderId);
            invitation.Cancel(_timeProvider.GetUtcNow());
        }
        else
        {
            UserContext.EnsureOwner(userId, invitation.ReceiverId);
            if (accept)
            {
                invitation.Accept(_timeProvider.GetUtcNow());
                await _notifications.AddAsync(new Notification(
                    invitation.SenderId,
                    NotificationType.RoommateInvitationAccepted,
                    "Lời mời ở ghép được chấp nhận",
                    "Người bạn mời đã đồng ý ở ghép.",
                    invitation.Id,
                    _timeProvider.GetUtcNow()), cancellationToken);
            }
            else
            {
                invitation.Reject(_timeProvider.GetUtcNow());
            }
        }

        await _invitations.SaveChangesAsync(cancellationToken);
        return RoommateInvitationMapper.ToDto(invitation);
    }
}
