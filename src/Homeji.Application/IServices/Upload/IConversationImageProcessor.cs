using Homeji.Application.DTOs.Conversations;

namespace Homeji.Application.IServices.Upload;

public interface IConversationImageProcessor
{
    Task<ProcessedConversationImage> ProcessAsync(
        ConversationImageUpload upload,
        CancellationToken cancellationToken = default);
}
