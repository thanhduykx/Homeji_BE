using System.Text.RegularExpressions;
using Homeji.Application.Services.AI;

namespace Homeji.Application.Services.Chatbot;

/// <summary>Free-form generation has no publishing tool result. Its prose must
/// not impersonate the authoritative confirmation endpoint.</summary>
public static class ChatbotActionClaims
{
    public static string WithoutUnverifiedPublishing(string reply)
    {
        var text = RentalSearchIntent.Normalize(reply);
        var completed = Regex.IsMatch(text,
            @"\b(?:(?:minh|toi|homeji|agent(?: premium)?)\s+(?:da\s+)(?:tu dong\s+)?(?:dang|gui tin|gui bai)|(?:tin|bai)(?:\s+(?:cua ban|dang|ban))?\s+(?:da\s+)(?:duoc\s+)?(?:gui|dang|dua vao)|noi dung va hinh anh[^\n.]{0,120}da duoc dua vao)\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return completed
            ? "Lượt chat này chưa thực hiện thao tác đăng bài. Mình cần tạo bản nháp thật để bạn xem ảnh, nội dung và giá; bài chỉ được đăng sau khi bạn bấm Xác nhận đăng bài và máy chủ trả kết quả. Bạn có thể nhắn ‘đăng bán’ kèm món đồ và giá để mình soạn bản nháp."
            : reply;
    }
}
