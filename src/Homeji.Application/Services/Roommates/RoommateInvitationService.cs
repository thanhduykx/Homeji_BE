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
    private readonly IRoommateDirectoryRepository _directory;
    private readonly Homeji.Application.IRepositories.Profiles.IUserProfileRepository _profiles;
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
        INotificationRealtimePublisher realtimePublisher,
        IRoommateDirectoryRepository directory,
        Homeji.Application.IRepositories.Profiles.IUserProfileRepository profiles)
    {
        _userContext = userContext;
        _invitations = invitations;
        _directory = directory;
        _profiles = profiles;
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

        var receiver = await _directory.GetAsync(request.ReceiverId, cancellationToken);
        if (receiver is null || !receiver.IsDiscoverable || (await _profiles.GetByIdAsync(request.ReceiverId, cancellationToken))?.Role != UserRole.Renter)
            throw new ForbiddenAccessException("Người nhận chưa bật tìm bạn ở ghép.");

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

    public async Task<RoommateInvitationDto> CreateIndependentAsync(CreateRoommateInvitationDto request, CancellationToken cancellationToken = default)
    {
        var sender = await GetRequiredRenterAsync(cancellationToken);
        if (request.ReceiverId == Guid.Empty || sender.Id == request.ReceiverId)
            throw new ForbiddenAccessException("Người nhận lời mời không hợp lệ.");
        var receiver = await _directory.GetAsync(request.ReceiverId, cancellationToken);
        if (receiver is null || !receiver.IsDiscoverable || (await _profiles.GetByIdAsync(request.ReceiverId, cancellationToken))?.Role != UserRole.Renter)
            throw new ForbiddenAccessException("Người nhận chưa bật tìm bạn ở ghép.");
        var existing = await _invitations.GetActiveIndependentAsync(sender.Id, request.ReceiverId, cancellationToken);
        if (existing is not null)
        {
            var chat = await _directConversations.FindAsync(ConversationSubjectType.RoommateInvitation, existing.Id,
                existing.SenderId, existing.ReceiverId, cancellationToken);
            return RoommateInvitationMapper.ToDto(existing, "Kết nối ở ghép", chat?.Id);
        }
        var invitation = new RoommateInvitation(null, sender.Id, request.ReceiverId, _timeProvider.GetUtcNow());
        await _invitations.AddAsync(invitation, cancellationToken);
        var notification = new Notification(request.ReceiverId, NotificationType.RoommateInvitationReceived,
            "Lời mời ở ghép mới", "Có người muốn kết nối ở ghép với bạn.", invitation.Id, _timeProvider.GetUtcNow());
        await _notifications.AddAsync(notification, cancellationToken);
        try { await _invitations.SaveChangesAsync(cancellationToken); }
        catch (RequestValidationException)
        {
            // A concurrent reciprocal request may have won the unique pair constraint.
            existing = await _invitations.GetActiveIndependentAsync(sender.Id, request.ReceiverId, cancellationToken);
            if (existing is null) throw;
            var chat = await _directConversations.FindAsync(ConversationSubjectType.RoommateInvitation, existing.Id,
                existing.SenderId, existing.ReceiverId, cancellationToken);
            return RoommateInvitationMapper.ToDto(existing, "Kết nối ở ghép", chat?.Id);
        }
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return RoommateInvitationMapper.ToDto(invitation, "Kết nối ở ghép");
    }

    public async Task<IReadOnlyList<RoommateInvitationDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var renter = await GetRequiredRenterAsync(cancellationToken);
        var userId = renter.Id;
        var invitations = await _invitations.GetForUserAsync(userId, cancellationToken);
        var postIds = invitations.Where(x => x.RentalPostId.HasValue).Select(x => x.RentalPostId!.Value).Distinct().ToArray();
        var posts = await _posts.GetByIdsAsync(postIds, cancellationToken);
        var postTitles = posts.ToDictionary(post => post.Id, post => post.Title);
        var directConversations = await _directConversations.GetForUserAsync(userId, cancellationToken);
        var names = (await _profiles.GetByIdsAsync(invitations.SelectMany(x => new[] { x.SenderId, x.ReceiverId }).Distinct().ToArray(), cancellationToken)).ToDictionary(x => x.Id, x => x.DisplayName);

        return invitations.Select(invitation =>
        {
            var conversationId = directConversations.FirstOrDefault(conversation =>
                conversation.SubjectType == SubjectType(invitation)
                && conversation.SubjectId == SubjectId(invitation)
                && conversation.Includes(invitation.SenderId)
                && conversation.Includes(invitation.ReceiverId))?.Id;
            return RoommateInvitationMapper.ToDto(
                invitation,
                invitation.RentalPostId is { } postId ? postTitles.GetValueOrDefault(postId) ?? "Tin đăng không còn khả dụng" : "Kết nối ở ghép",
                conversationId) with { SenderDisplayName = names.GetValueOrDefault(invitation.SenderId), ReceiverDisplayName = names.GetValueOrDefault(invitation.ReceiverId) };
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
        var post = invitation.RentalPostId is { } rentalPostId ? await _posts.GetByIdAsync(rentalPostId, cancellationToken) : null;

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
                if (invitation.RentalPostId.HasValue && await _conversations.GetByInvitationIdAsync(invitation.Id, cancellationToken) is null)
                {
                    await _conversations.AddConversationAsync(new RoommateConversation(
                        invitation.Id,
                        invitation.RentalPostId!.Value,
                        invitation.SenderId,
                        invitation.ReceiverId,
                        _timeProvider.GetUtcNow()), cancellationToken);
                }

                directConversation = await _directConversations.FindAsync(
                    SubjectType(invitation),
                    SubjectId(invitation),
                    invitation.SenderId,
                    invitation.ReceiverId,
                    cancellationToken);
                if (directConversation is null)
                {
                    directConversation = new PostConversation(
                        SubjectType(invitation),
                        SubjectId(invitation),
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
            SubjectType(invitation),
            SubjectId(invitation),
            invitation.SenderId,
            invitation.ReceiverId,
            cancellationToken);
        return RoommateInvitationMapper.ToDto(
            invitation,
            post?.Title ?? (invitation.RentalPostId is null ? "Kết nối ở ghép" : "Tin đăng không còn khả dụng"),
            directConversation?.Id);
    }

    private static ConversationSubjectType SubjectType(RoommateInvitation invitation) =>
        invitation.RentalPostId.HasValue ? ConversationSubjectType.RentalPost : ConversationSubjectType.RoommateInvitation;
    private static Guid SubjectId(RoommateInvitation invitation) => invitation.RentalPostId ?? invitation.Id;

    private async Task<UserProfile> GetRequiredRenterAsync(CancellationToken cancellationToken)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(profile);
        return profile;
    }
}
