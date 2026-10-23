using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.IServices.Chatbot;

public interface IChatbotFoodService
{
    Task<ChatbotFoodResponseDto> SearchAsync(string message, ChatbotFoodContextDto? origin,
        CancellationToken cancellationToken = default);
}
