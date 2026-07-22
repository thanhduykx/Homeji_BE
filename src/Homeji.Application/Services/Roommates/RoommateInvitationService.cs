using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Roommates;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IRepositories.RoommateChats;
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
    private readonly IRoommateConversationRepository _conversations;
    private readonly IPostConversationRepository _directConversations;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher _realtimePublisher;

    public RoommateInvitationService(
        UserContext userContext,
        IRoommateInvitationRepository invitations,
        ISavedPostRepository savedPosts,
        IRentalPostRepository posts,
        INotificationRepository notifications,
        IRoommateConversationRepository conversations,
        IPostConversationRepository directConversations,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher)
    {
        _userContext = userContext;
        _invitations = invitations;
        _savedPosts = savedPosts;
        _posts = posts;
        _notifications = notifications;
        _conversations = conversations;
        _directConversations = directConversations;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
    }

    public async Task<RoommateInvitationDto> CreateAsync(
        Guid postId,
        CreateRoommateInvitationDto request,
        CancellationToken cancellationToken = default)
    {
        var sender = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(sender);
        var senderId = sender.Id;
        if (senderId == request.ReceiverId)
        {
            throw new ForbiddenAccessException("Bạn không thể mời chính mình.");
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
            throw new ForbiddenAccessException("Cả hai người phải lưu tin đăng này trước khi gửi lời mời ở ghép.");
        }

        if (await _invitations.HasPendingAsync(postId, senderId, request.ReceiverId, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["receiverId"] = ["Đã có lời mời đang chờ xử lý."],
            });
        }

        var invitation = new RoommateInvitation(postId, senderId, request.ReceiverId, _timeProvider.GetUtcNow());
        await _invitations.AddAsync(invitation, cancellationToken);
        var notification = new Notification(
            request.ReceiverId,
            NotificationType.RoommateInvitationReceived,
            "Lời mời ở ghép mới",
            "Có người cũng quan tâm phòng này muốn ở ghép với bạn.",
            invitation.Id,
            _timeProvider.GetUtcNow());
        await _notifications.AddAsync(notification, cancellationToken);
        await _invitations.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return RoommateInvitationMapper.ToDto(invitation, post.Title);
    }

    public async Task<IReadOnlyList<RoommateInvitationDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var renter = await GetRequiredRenterAsync(cancellationToken);
        var userId = renter.Id;
        var invitations = await _invitations.GetForUserAsync(userId, cancellationToken);
        var postIds = invitations.Select(invitation => invitation.RentalPostId).Distinct().ToArray();
        var posts = await _posts.GetByIdsAsync(postIds, cancellationToken);
        var postTitles = posts.ToDictionary(post => post.Id, post => post.Title);
        var directConversations = await _directConversations.GetForUserAsync(userId, cancellationToken);

        return invitations.Select(invitation =>
        {
            var conversationId = directConversations.FirstOrDefault(conversation =>
                conversation.SubjectType == ConversationSubjectType.RentalPost
                && conversation.SubjectId == invitation.RentalPostId
                && conversation.Includes(invitation.SenderId)
                && conversation.Includes(invitation.ReceiverId))?.Id;
            return RoommateInvitationMapper.ToDto(
                invitation,
                postTitles.GetValueOrDefault(invitation.RentalPostId) ?? "Tin đăng không còn khả dụng",
                conversationId);
        }).ToArray();
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
        var renter = await GetRequiredRenterAsync(cancellationToken);
        var userId = renter.Id;
        Notification? notification = null;
        PostConversation? directConversation = null;
        var invitation = await _invitations.GetByIdAsync(invitationId, cancellationToken)
            ?? throw new NotFoundException(nameof(RoommateInvitation), invitationId);
        var post = await _posts.GetByIdAsync(invitation.RentalPostId, cancellationToken);

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
                if (await _conversations.GetByInvitationIdAsync(invitation.Id, cancellationToken) is null)
                {
                    await _conversations.AddConversationAsync(new RoommateConversation(
                        invitation.Id,
                        invitation.RentalPostId,
                        invitation.SenderId,
                        invitation.ReceiverId,
                        _timeProvider.GetUtcNow()), cancellationToken);
                }

                directConversation = await _directConversations.FindAsync(
                    ConversationSubjectType.RentalPost,
                    invitation.RentalPostId,
                    invitation.SenderId,
                    invitation.ReceiverId,
                    cancellationToken);
                if (directConversation is null)
                {
                    directConversation = new PostConversation(
                        ConversationSubjectType.RentalPost,
                        invitation.RentalPostId,
                        invitation.SenderId,
                        invitation.ReceiverId,
                        _timeProvider.GetUtcNow());
                    await _directConversations.AddConversationAsync(directConversation, cancellationToken);
                }

                notification = new Notification(
                    invitation.SenderId,
                    NotificationType.RoommateInvitationAccepted,
                    "Lời mời ở ghép được chấp nhận",
                    "Người bạn mời đã đồng ý ở ghép.",
                    invitation.Id,
                    _timeProvider.GetUtcNow());
                await _notifications.AddAsync(notification, cancellationToken);
            }
            else
            {
                invitation.Reject(_timeProvider.GetUtcNow());
            }
        }

        await _invitations.SaveChangesAsync(cancellationToken);
        if (notification is not null)
        {
            await _realtimePublisher.PublishAsync(notification, cancellationToken);
        }

        directConversation ??= await _directConversations.FindAsync(
            ConversationSubjectType.RentalPost,
            invitation.RentalPostId,
            invitation.SenderId,
            invitation.ReceiverId,
            cancellationToken);
        return RoommateInvitationMapper.ToDto(
            invitation,
            post?.Title ?? "Tin đăng không còn khả dụng",
            directConversation?.Id);
    }

    private async Task<UserProfile> GetRequiredRenterAsync(CancellationToken cancellationToken)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(profile);
        return profile;
    }
}
