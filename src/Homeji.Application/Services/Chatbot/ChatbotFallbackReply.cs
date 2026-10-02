using Homeji.Application.DTOs.Chatbot;

namespace Homeji.Application.Services.Chatbot;

public static class ChatbotFallbackReply
{
    public static string Create(IReadOnlyCollection<ChatbotNavigationActionDto> actions)
    {
        if (actions.Any(action => action.Id == "marketplace-cart"))
        {
            return "Mình có thể mở **giỏ hàng** để bạn kiểm tra món, số lượng và tổng tiền. "
                + "Homeji chỉ tạo đơn sau khi bạn bấm xác nhận.";
        }

        if (actions.Any(action => action.Id == "marketplace-food"))
        {
            return "Mình có thể mở **Chợ đồ ăn** để bạn chọn món và thêm vào giỏ. "
                + "Trước khi tạo đơn, Homeji sẽ cho bạn kiểm tra và xác nhận tổng tiền.";
        }

        if (actions.Count > 0)
        {
            return "Mình đang dùng chế độ hỗ trợ cơ bản. Bạn có thể bấm nút bên dưới để mở đúng tính năng trong Homeji.";
        }

        return "Mình đang dùng chế độ hỗ trợ cơ bản. Bạn hãy mô tả tính năng Homeji cần mở, "
            + "ví dụ: tìm phòng, đồ ăn, giỏ hàng, lịch xem phòng hoặc hồ sơ.";
    }
}
