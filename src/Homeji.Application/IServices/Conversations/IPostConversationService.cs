using Homeji.Application.DTOs.Conversations;

namespace Homeji.Application.IServices.Conversations;

public interface IPostConversationService
{
    Task<PostConversationDto> StartRentalConversationAsync(Guid rentalPostId, CancellationToken cancellationToken = default);
    Task<PostConversationDto> StartMarketplaceConversationAsync(Guid marketplacePostId, CancellationToken cancellationToken = default);
    Task<PostConversationDto> StartWantedPostConversationAsync(Guid wantedPostId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostConversationDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PostMessageDto>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken = default);
    Task<PostMessageDto> SendMessageAsync(Guid conversationId, SendPostMessageDto request, CancellationToken cancellationToken = default);
}
