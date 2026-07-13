namespace Homeji.Application.DTOs.RoommateChats;

public sealed record RoommateMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string Body,
    DateTimeOffset SentAt);
