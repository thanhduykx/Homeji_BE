using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.IServices.Chatbot;

public interface IChatbotNearbyService
{
    Task<ChatbotNearbyDto> SearchAsync(string message, ChatbotLocationDto? location, CancellationToken cancellationToken);
}
