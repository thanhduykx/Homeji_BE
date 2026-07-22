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
using Homeji.Application.IServices.Upload;
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
    private readonly IConversationImageProcessor _imageProcessor;

    public PostConversationService(
        UserContext userContext,
        IPostConversationRepository conversations,
        IRentalPostRepository rentalPosts,
        IMarketplacePostRepository marketplacePosts,
        IRentalWantedPostRepository wantedPosts,
        IUserProfileRepository profiles,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider,
        IConversationImageProcessor imageProcessor)
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
        _imageProcessor = imageProcessor;
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

    public async Task<PostMessageDto> SendImagesAsync(
        Guid conversationId,
        string? body,
        IReadOnlyList<ConversationImageUpload> images,
        CancellationToken cancellationToken = default)
    {
        if (images is null || images.Count is < 1 or > 5)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["files"] = ["Select between 1 and 5 images."],
            });
        }

        if (!string.IsNullOrWhiteSpace(body))
        {
            ValidateBody(body);
        }

        var senderId = _userContext.GetRequiredUserId();
        var conversation = await GetAuthorizedAsync(conversationId, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var recentCount = await _conversations.CountAttachmentsByUploaderSinceAsync(
            senderId,
            now.AddHours(-24),
            cancellationToken);
        if (recentCount + images.Count > 30)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["files"] = ["You can send at most 30 chat images in 24 hours."],
            });
        }

        var processed = new List<(ConversationImageUpload Upload, ProcessedConversationImage Image)>(images.Count);
        foreach (var image in images)
        {
            if (!Enum.IsDefined(image.Context))
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["context"] = ["Image context is invalid."],
                });
            }

            try
            {
                processed.Add((image, await _imageProcessor.ProcessAsync(image, cancellationToken)));
            }
            catch (InvalidOperationException exception)
            {
                throw new RequestValidationException(new Dictionary<string, string[]>
                {
                    ["files"] = [exception.Message],
                });
            }
        }

        var messageBody = string.IsNullOrWhiteSpace(body)
            ? $"Đã gửi {images.Count} ảnh"
            : body.Trim();
        var message = new PostMessage(conversation.Id, senderId, messageBody, now);
        foreach (var item in processed)
        {
            message.AddImage(
                senderId,
                item.Upload.Context,
                item.Image.MimeType,
                item.Image.Content,
                item.Image.Width,
                item.Image.Height,
                item.Image.Sha256,
                now);
        }

        conversation.Touch(now);
        conversation.MarkRead(senderId, now);
        await _conversations.AddMessageAsync(message, cancellationToken);
        var notification = CreateMessageNotification(conversation.GetOtherParticipantId(senderId), conversation.Id, now);
        await _notifications.AddAsync(notification, cancellationToken);
        await _conversations.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(message);
    }

    public async Task<PostMessageAttachmentContentDto> GetAttachmentContentAsync(
        Guid conversationId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        await GetAuthorizedAsync(conversationId, cancellationToken);
        var attachment = await _conversations.GetAttachmentAsync(
            conversationId,
            messageId,
            attachmentId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(PostMessageAttachment), attachmentId);
        if (attachment.Status != MessageAttachmentStatus.Ready || attachment.Content.Length == 0)
        {
            throw new NotFoundException(nameof(PostMessageAttachment), attachmentId);
        }

        return new PostMessageAttachmentContentDto(
            attachment.MimeType,
            attachment.Content,
            attachment.Sha256);
    }

    public async Task DeleteAttachmentAsync(
        Guid conversationId,
        Guid messageId,
        Guid attachmentId,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        await GetAuthorizedAsync(conversationId, cancellationToken);
        var attachment = await _conversations.GetAttachmentAsync(
            conversationId,
            messageId,
            attachmentId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(PostMessageAttachment), attachmentId);
        if (attachment.UploaderId != userId)
        {
            throw new ForbiddenAccessException("Only the uploader can delete this attachment.");
        }

        attachment.Delete(userId, _timeProvider.GetUtcNow());
        await _conversations.SaveChangesAsync(cancellationToken);
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
            throw new ForbiddenAccessException("Bạn không thể tự nhắn tin với chính mình.");
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
            throw new ForbiddenAccessException("Bạn không phải thành viên của cuộc trò chuyện này.");
        }

        return conversation;
    }

    private static void ValidateBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > PostMessage.MaxBodyLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["body"] = [$"Nội dung tin nhắn là bắt buộc và không quá {PostMessage.MaxBodyLength} ký tự."],
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
        var attachments = message.Attachments
            .OrderBy(attachment => attachment.CreatedAt)
            .Select(attachment => new PostMessageAttachmentDto(
                attachment.Id,
                attachment.UploaderId,
                attachment.Context,
                attachment.Status,
                attachment.MimeType,
                attachment.Bytes,
                attachment.Width,
                attachment.Height,
                $"/api/conversations/{message.ConversationId:D}/messages/{message.Id:D}/attachments/{attachment.Id:D}/content",
                attachment.CreatedAt,
                attachment.DeletedAt))
            .ToArray();
        return new PostMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.Body,
            message.SentAt,
            attachments);
    }

    private static Notification CreateMessageNotification(
        Guid recipientId,
        Guid conversationId,
        DateTimeOffset createdAt)
    {
        return new Notification(
            recipientId,
            NotificationType.DirectMessage,
            "Tin nhắn mới",
            "Bạn có tin nhắn mới liên quan đến một bài đăng.",
            conversationId,
            createdAt);
    }
}
