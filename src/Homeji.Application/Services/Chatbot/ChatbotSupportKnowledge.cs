using System.Text.RegularExpressions;

namespace Homeji.Application.Services.Chatbot;

/// <summary>Reviewed product instructions. Never infers live payment or account state.</summary>
public static partial class ChatbotSupportKnowledge
{
    public static string? FindReply(string message)
    {
        var text = Whitespace().Replace(ChatbotNavigationCatalog.Normalize(message).Replace('đ', 'd'), " ")
            .Trim(' ', '.', '!', '?', ',', ':', ';');

        if (text is "hi" or "hello" or "hey" or "xin chao" or "chao" or "chao ban" or "chao homeji")
            return "Xin chào! Mình là trợ lý Homeji. Bạn muốn tìm phòng, tìm bạn ở ghép, mua đồ hay tìm hiểu thanh toán? Nếu tìm phòng, hãy cho mình ngân sách và tiện ích bạn cần.";
        if (text is "cam on" or "cam on ban" or "thanks" or "thank you")
            return "Rất vui được hỗ trợ bạn! Bạn còn cần tìm phòng hoặc hướng dẫn tính năng nào trong Homeji không?";
        if (text is "ban la ai" or "ban giup duoc gi" or "help" or "tro giup")
            return "Mình là trợ lý Homeji. Mình có thể tìm phòng theo ngân sách và tiện ích, hướng dẫn ở ghép, Chợ đồ và thanh toán gói. Bạn đang cần hỗ trợ việc nào?";

        if (HasAny(text, "mua do an", "dat do an", "dat mon") && HasAny(text, "cach", "nhu the nao", "huong dan"))
            return "Để mua đồ ăn, mở **Chợ đồ ăn**, xem tin và chọn món/số lượng để thêm vào giỏ. Kiểm tra món, số lượng và tổng tiền trong **giỏ hàng**, rồi tự xác nhận tạo đơn. Sau đó theo dõi đơn trong khu vực Chợ đồ. Mình không tạo đơn hoặc xác nhận thanh toán chỉ từ tin nhắn của bạn.";

        if (text.Contains("payos", StringComparison.Ordinal))
        {
            if (HasAny(text, "da thanh toan", "da tra", "bi tru", "chua kich hoat", "khong kich hoat", "chua nhan", "hoan tien"))
                return "Mình chưa có dữ liệu giao dịch của bạn để xác nhận đã thu tiền, kích hoạt gói hay hoàn tiền. Mở **Gói đăng ký → Lịch sử giao dịch**, chọn giao dịch cần kiểm tra. Ở trang chờ, bấm **Tôi đã thanh toán · Kiểm tra lại** để lấy trạng thái từ hệ thống. Nếu vẫn chưa cập nhật, hãy giữ thông tin giao dịch để đối chiếu; đừng tạo thanh toán mới ngay. Không gửi mật khẩu hoặc OTP vào chat.";
            if (HasAny(text, "cach", "huong dan", "nhu the nao", "thanh toan bang", "thanh toan qua") || text == "payos")
                return "Bạn có thể thanh toán **gói Premium bằng PayOS** trong Homeji như sau:\n\n1. Mở **Gói đăng ký**, xem quyền lợi và chọn gói phù hợp.\n2. Chọn **PayOS** để tạo giao dịch.\n3. Tại trang chờ, bấm **Thanh toán qua PayOS** và làm theo hướng dẫn của cổng thanh toán; kiểm tra số tiền trước khi xác nhận.\n4. Quay lại Homeji và chờ hệ thống cập nhật. Nếu chưa cập nhật, bấm **Tôi đã thanh toán · Kiểm tra lại**.\n\nGiao dịch chưa thanh toán có thời hạn 15 phút. Gói chỉ được kích hoạt khi hệ thống xác nhận thanh toán thành công. Không cung cấp mật khẩu hoặc OTP cho chatbot.";
            return "Bạn cần hướng dẫn thanh toán gói bằng PayOS, hay kiểm tra một giao dịch đã thanh toán? Mình có thể hướng dẫn thao tác; trạng thái thực tế cần xem ở **Gói đăng ký → Lịch sử giao dịch**.";
        }

        if (HasAny(text, "premium", "goi dang ky") && HasAny(text, "loi ich", "quyen loi", "gia", "bao nhieu", "nang cap"))
            return "Mở **Gói đăng ký** để xem quyền lợi, giá và thời hạn hiện tại của từng gói Premium. Chọn gói phù hợp rồi chọn phương thức thanh toán. Mình không tự nâng cấp hay thu tiền từ cuộc trò chuyện; chỉ hệ thống thanh toán mới xác nhận giao dịch và kích hoạt gói.";

        return null;
    }

    private static bool HasAny(string text, params string[] terms) => terms.Any(term => text.Contains(term, StringComparison.Ordinal));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
