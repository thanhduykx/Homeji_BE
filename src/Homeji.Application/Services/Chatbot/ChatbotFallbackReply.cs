using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.Services.Chatbot;

public static class ChatbotFallbackReply
{
    public static string Create(IReadOnlyCollection<ChatbotNavigationActionDto> actions)
    {
        const string unavailable = "AI đang tạm thời không khả dụng. Bạn vui lòng thử lại sau. ";
        if (actions.Any(action => action.Id == "marketplace-cart"))
        {
            return unavailable + "Bạn vẫn có thể mở **giỏ hàng** để kiểm tra món, số lượng và tổng tiền. "
                + "Homeji chỉ tạo đơn sau khi bạn bấm xác nhận.";
        }

        if (actions.Any(action => action.Id == "marketplace-food"))
        {
            return unavailable + "Bạn vẫn có thể mở **Chợ đồ ăn** để chọn món và thêm vào giỏ. "
                + "Trước khi tạo đơn, Homeji sẽ cho bạn kiểm tra và xác nhận tổng tiền.";
        }

        if (actions.Count > 0)
        {
            return unavailable + "Bạn vẫn có thể bấm nút bên dưới để mở đúng tính năng trong Homeji.";
        }

        return unavailable + "Mình chưa thể trả lời câu hỏi này và sẽ không thay bằng câu trả lời đoán.";
    }
}
