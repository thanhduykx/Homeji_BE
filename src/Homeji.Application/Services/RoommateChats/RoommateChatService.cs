using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RoommateChats;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.RoommateChats;
using Homeji.Application.IServices.RoommateChats;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.RoommateChats;

public sealed class RoommateChatService : IRoommateChatService
{
    private readonly UserContext _userContext;
    private readonly IRoommateConversationRepository _conversations;
    private readonly IUserProfileRepository _profiles;
    private readonly INotificationRepository _notifications;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher _realtimePublisher;

    public RoommateChatService(
        UserContext userContext,
        IRoommateConversationRepository conversations,
        IUserProfileRepository profiles,
        INotificationRepository notifications,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher)
    {
        _userContext = userContext;
        _conversations = conversations;
        _profiles = profiles;
        _notifications = notifications;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
    }

    public async Task<IReadOnlyList<RoommateConversationDto>> GetMineAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversations = await _conversations.GetForUserAsync(userId, cancellationToken);
        var otherIds = conversations
            .Select(conversation => conversation.GetOtherParticipantId(userId))
            .Distinct()
            .ToArray();
        var profiles = await _profiles.GetByIdsAsync(otherIds, cancellationToken);
        var profilesById = profiles.ToDictionary(profile => profile.Id);

        return conversations.Select(conversation =>
        {
            var otherId = conversation.GetOtherParticipantId(userId);
            var profile = profilesById.GetValueOrDefault(otherId);
            return new RoommateConversationDto(
                conversation.Id,
                conversation.InvitationId,
                conversation.RentalPostId,
                otherId,
                profile?.DisplayName ?? "Homeji user",
                profile?.AvatarPath,
                conversation.CreatedAt,
                conversation.UpdatedAt);
        }).ToArray();
    }

    public async Task<IReadOnlyList<RoommateMessageDto>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var conversation = await GetAuthorizedConversationAsync(conversationId, cancellationToken);
        var messages = await _conversations.GetMessagesAsync(conversation.Id, cancellationToken);
        return messages.Select(ToDto).ToArray();
    }

    public async Task<RoommateMessageDto> SendMessageAsync(
        Guid conversationId,
        SendRoommateMessageDto request,
        CancellationToken cancellationToken = default)
    {
        var senderId = _userContext.GetRequiredUserId();
        var conversation = await GetAuthorizedConversationAsync(conversationId, cancellationToken);
        ValidateBody(request.Body);

        var now = _timeProvider.GetUtcNow();
        var message = new RoommateMessage(conversation.Id, senderId, request.Body!, now);
        await _conversations.AddMessageAsync(message, cancellationToken);
        conversation.Touch(now);

        var recipientId = conversation.GetOtherParticipantId(senderId);
        var notification = new Notification(
            recipientId,
            NotificationType.NewMessage,
            "Tin nhắn ở ghép mới",
            "Bạn có một tin nhắn mới từ người ở ghép.",
            conversation.Id,
            now);
        await _notifications.AddAsync(notification, cancellationToken);

        await _conversations.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(message);
    }

    private async Task<RoommateConversation> GetAuthorizedConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversation = await _conversations.GetByIdAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(RoommateConversation), conversationId);
        if (!conversation.Includes(userId))
        {
            throw new ForbiddenAccessException("You are not a participant in this roommate conversation.");
        }

        return conversation;
    }

    private static void ValidateBody(string? body)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(body))
        {
            errors["body"] = ["Message body is required."];
        }
        else if (body.Trim().Length > RoommateMessage.MaxBodyLength)
        {
            errors["body"] = [$"Message body must not exceed {RoommateMessage.MaxBodyLength} characters."];
        }

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors);
        }
    }

    private static RoommateMessageDto ToDto(RoommateMessage message)
    {
        return new RoommateMessageDto(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.Body,
            message.SentAt);
    }
}
