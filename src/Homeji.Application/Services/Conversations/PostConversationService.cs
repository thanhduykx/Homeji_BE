using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Conversations;
using Homeji.Application.IRepositories.Conversations;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Application.IServices.Conversations;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Conversations;

public sealed class PostConversationService : IPostConversationService
{
    private readonly UserContext _userContext;
    private readonly IPostConversationRepository _conversations;
    private readonly IRentalPostRepository _rentalPosts;
    private readonly IMarketplacePostRepository _marketplacePosts;
    private readonly IUserProfileRepository _profiles;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly TimeProvider _timeProvider;
    private readonly IRentalWantedPostRepository _wantedPosts;

    public PostConversationService(
        UserContext userContext,
        IPostConversationRepository conversations,
        IRentalPostRepository rentalPosts,
        IMarketplacePostRepository marketplacePosts,
        IRentalWantedPostRepository wantedPosts,
        IUserProfileRepository profiles,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _conversations = conversations;
        _rentalPosts = rentalPosts;
        _marketplacePosts = marketplacePosts;
        _wantedPosts = wantedPosts;
        _profiles = profiles;
        _notifications = notifications;
        _realtimePublisher = realtimePublisher;
        _timeProvider = timeProvider;
    }

    public async Task<PostConversationDto> StartRentalConversationAsync(
        Guid rentalPostId,
        CancellationToken cancellationToken = default)
    {
        var renter = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(renter);
        var post = await _rentalPosts.GetByIdAsync(rentalPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), rentalPostId);
        if (post.Status != RentalPostStatus.Active)
        {
            throw new NotFoundException(nameof(RentalPost), rentalPostId);
        }

        return await StartAsync(ConversationSubjectType.RentalPost, post.Id, post.OwnerId, cancellationToken);
    }

    public async Task<PostConversationDto> StartMarketplaceConversationAsync(
        Guid marketplacePostId,
        CancellationToken cancellationToken = default)
    {
        var post = await _marketplacePosts.GetByIdWithMediaAsync(marketplacePostId, cancellationToken)
            ?? throw new NotFoundException(nameof(MarketplacePost), marketplacePostId);
        if (post.Status != MarketplacePostStatus.Active)
        {
            throw new NotFoundException(nameof(MarketplacePost), marketplacePostId);
        }

        return await StartAsync(ConversationSubjectType.MarketplacePost, post.Id, post.SellerId, cancellationToken);
    }

    public async Task<PostConversationDto> StartWantedPostConversationAsync(
        Guid wantedPostId,
        CancellationToken cancellationToken = default)
    {
        _userContext.GetRequiredUserId();
        var post = await _wantedPosts.GetByIdAsync(wantedPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalWantedPost), wantedPostId);
        if (post.Status != WantedPostStatus.Active)
        {
            throw new NotFoundException(nameof(RentalWantedPost), wantedPostId);
        }

        return await StartAsync(ConversationSubjectType.WantedPost, post.Id, post.RequesterId, cancellationToken);
    }

    public async Task<IReadOnlyList<PostConversationDto>> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversations = await _conversations.GetForUserAsync(userId, cancellationToken);
        var otherIds = conversations.Select(conversation => conversation.GetOtherParticipantId(userId)).Distinct().ToArray();
        var profiles = await _profiles.GetByIdsAsync(otherIds, cancellationToken);
        var profileMap = profiles.ToDictionary(profile => profile.Id);
        var conversationIds = conversations.Select(conversation => conversation.Id).ToArray();
        var lastMessages = await _conversations.GetLatestByConversationIdsAsync(conversationIds, cancellationToken);
        var unreadCounts = await _conversations.CountUnreadByConversationIdsAsync(userId, conversations, cancellationToken);
        return conversations.Select(conversation =>
        {
            lastMessages.TryGetValue(conversation.Id, out var lastMessage);
            return ToDto(
                conversation,
                userId,
                profileMap.GetValueOrDefault(conversation.GetOtherParticipantId(userId)),
                lastMessage,
                unreadCounts.GetValueOrDefault(conversation.Id));
        }).ToArray();
    }

    public async Task<IReadOnlyList<PostMessageDto>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversation = await GetAuthorizedAsync(conversationId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        conversation.MarkRead(userId, now);
        await _notifications.MarkDirectMessagesReadAsync(userId, conversation.Id, now, cancellationToken);
        await _conversations.SaveChangesAsync(cancellationToken);

        var messages = await _conversations.GetMessagesAsync(conversation.Id, cancellationToken);
        return messages.Select(ToDto).ToArray();
    }

    public async Task<PostMessageDto> SendMessageAsync(
        Guid conversationId,
        SendPostMessageDto request,
        CancellationToken cancellationToken = default)
    {
        ValidateBody(request.Body);
        var senderId = _userContext.GetRequiredUserId();
        var conversation = await GetAuthorizedAsync(conversationId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var message = new PostMessage(conversation.Id, senderId, request.Body!, now);
        conversation.Touch(now);
        conversation.MarkRead(senderId, now);
        await _conversations.AddMessageAsync(message, cancellationToken);

        var recipientId = conversation.GetOtherParticipantId(senderId);
        var notification = new Notification(
            recipientId,
            NotificationType.DirectMessage,
            "Tin nhắn mới",
            "Bạn có tin nhắn mới liên quan đến một bài đăng.",
            conversation.Id,
            now);
        await _notifications.AddAsync(notification, cancellationToken);
        await _conversations.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(message);
    }

    private async Task<PostConversationDto> StartAsync(
        ConversationSubjectType subjectType,
        Guid subjectId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var requesterId = _userContext.GetRequiredUserId();
        if (requesterId == ownerId)
        {
            throw new ForbiddenAccessException("You cannot start a conversation with yourself.");
        }

        var conversation = await _conversations.FindAsync(subjectType, subjectId, requesterId, ownerId, cancellationToken);
        if (conversation is null)
        {
            conversation = new PostConversation(subjectType, subjectId, requesterId, ownerId, _timeProvider.GetUtcNow());
            await _conversations.AddConversationAsync(conversation, cancellationToken);
            await _conversations.SaveChangesAsync(cancellationToken);
        }

        var profile = await _profiles.GetByIdAsync(ownerId, cancellationToken);
        return ToDto(conversation, requesterId, profile);
    }

    private async Task<PostConversation> GetAuthorizedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversation = await _conversations.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(PostConversation), id);
        if (!conversation.Includes(userId))
        {
            throw new ForbiddenAccessException("You are not a participant in this conversation.");
        }

        return conversation;
    }

    private static void ValidateBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > PostMessage.MaxBodyLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["body"] = [$"Message body is required and must not exceed {PostMessage.MaxBodyLength} characters."],
            });
        }
    }

    private static PostConversationDto ToDto(
        PostConversation conversation,
        Guid currentUserId,
        UserProfile? otherProfile,
        ConversationLastMessageDto? lastMessage = null,
        int unreadCount = 0)
    {
        var otherId = conversation.GetOtherParticipantId(currentUserId);
        return new PostConversationDto(
            conversation.Id,
            conversation.SubjectType,
            conversation.SubjectId,
            otherId,
            otherProfile?.DisplayName ?? "Homeji user",
            otherProfile?.AvatarPath,
            conversation.CreatedAt,
            conversation.UpdatedAt,
            lastMessage?.Body,
            lastMessage?.SenderId,
            unreadCount);
    }

    private static PostMessageDto ToDto(PostMessage message)
    {
        return new PostMessageDto(message.Id, message.ConversationId, message.SenderId, message.Body, message.SentAt);
    }
}
