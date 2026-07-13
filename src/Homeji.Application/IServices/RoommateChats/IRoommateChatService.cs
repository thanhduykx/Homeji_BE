using Homeji.Application.DTOs.RoommateChats;

namespace Homeji.Application.IServices.RoommateChats;

public interface IRoommateChatService
{
    Task<IReadOnlyList<RoommateConversationDto>> GetMineAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoommateMessageDto>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<RoommateMessageDto> SendMessageAsync(
        Guid conversationId,
        SendRoommateMessageDto request,
        CancellationToken cancellationToken = default);
}
