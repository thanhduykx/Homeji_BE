using Homeji.Domain.Enums;

namespace Homeji.Application.DTOs.Conversations;

public sealed record PostConversationDto(
    Guid Id,
    ConversationSubjectType SubjectType,
    Guid SubjectId,
    Guid OtherParticipantId,
    string OtherParticipantName,
    string? OtherParticipantAvatarPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? LastMessage = null,
    Guid? LastMessageSenderId = null,
    int UnreadCount = 0);

public sealed record PostMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    DateTimeOffset SentAt,
    IReadOnlyList<PostMessageAttachmentDto>? Attachments = null);

public sealed record PostMessageAttachmentDto(
    Guid Id,
    Guid UploaderId,
    MessageAttachmentContext Context,
    MessageAttachmentStatus Status,
    string MimeType,
    long Bytes,
    int Width,
    int Height,
    string ContentPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeletedAt);

public sealed record SendPostMessageDto(string? Body);

public sealed record ConversationLastMessageDto(
    Guid ConversationId,
    string Body,
    Guid SenderId,
    DateTimeOffset SentAt);
