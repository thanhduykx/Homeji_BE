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
    DateTimeOffset UpdatedAt);

public sealed record PostMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    DateTimeOffset SentAt);

public sealed record SendPostMessageDto(string? Body);
