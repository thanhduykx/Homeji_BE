using System.Globalization;
using System.Text.RegularExpressions;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.AI;

namespace Homeji.Application.Services.Chatbot;

internal static partial class ChatbotRentalContext
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static string? ResolveTask(ChatbotRentalContextDto? context, string message)
    {
        if (context is null) return null;
        if (context.PostId == Guid.Empty || context.Task is not (null or "cost" or "questions" or "details"))
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["rentalContext"] = ["Chọn mã tin hợp lệ và tác vụ cost, questions hoặc details."],
            });

        var normalized = RentalSearchIntent.Normalize(message);
        // A selected listing never changes an explicit request to start another search.
        if (NewSearch().IsMatch(normalized)) return null;
        if (context.Task is not null) return context.Task;
        if (OwnerQuestions().IsMatch(normalized)) return "questions";
        if (CostQuestion().IsMatch(normalized)) return "cost";
        return SelectedListing().IsMatch(normalized) ? "details" : null;
    }

    public static string BuildReply(string task, RentalDecisionResponseDto decision)
    {
        var item = decision.Posts.SingleOrDefault();
        if (item is null)
            return "Tin đang chọn không còn khả dụng để hỗ trợ. Hãy mở một tin công khai đang hoạt động rồi thử lại. Mình chưa gửi tin nhắn hay yêu cầu nào cho chủ tin.";
        if (task == "questions")
        {
            var questions = new List<string> { "Phòng hiện còn trống không và có thể vào ở từ ngày nào?" };
            if (item.Cost.PriceBasis == "unknown") questions.Add("Giá thuê tính theo phòng hay theo người mỗi tháng?");
            questions.AddRange(item.Cost.Questions);
            return "Bản nháp câu hỏi cho chủ tin, bạn có thể chỉnh trước khi gửi:\n\n"
                + string.Join('\n', questions.Select(question => "- " + question))
                + "\n\nMình chưa gửi tin nhắn này. Bạn có thể mở hội thoại để kiểm tra người nhận và gửi.";
        }

        var unit = item.Cost.PriceBasis switch
        {
            "room" => "đồng/phòng/tháng",
            "person" => "đồng/người/tháng",
            _ => "đồng/tháng (chưa xác nhận theo phòng hay theo người)",
        };
        var lines = new List<string>
        {
            item.Post.Price > 0 ? $"Tiền thuê theo tin: **{Money(item.Post.Price)} {unit}**." : "Tiền thuê chưa được xác nhận.",
        };
        if (task == "details")
        {
            if (item.Post.Area > 0) lines.Add($"Diện tích theo tin: {item.Post.Area.ToString("0.##", Vietnamese)} m².");
            lines.Add($"Số chỗ còn theo tin: {item.Post.AvailableSlots}. Cần hỏi chủ tin để xác nhận phòng thực sự còn trống.");
        }
        if (item.Post.Deposit > 0)
            lines.Add($"Tiền cọc theo tin: {Money(item.Post.Deposit)} đồng; cần xác nhận điều kiện và đơn vị cọc.");
        else lines.Add("Tiền cọc chưa xác nhận; số 0 chưa có nghĩa là miễn cọc.");
        lines.Add(item.Cost.EstimatedMonthlyTotal is { } total
            ? $"Tổng tháng theo kịch bản: {Money(total)} đồng."
            : "**Chưa đủ thông tin để kết luận tổng tiền mỗi tháng.** Hãy xác nhận đơn vị điện, nước, internet và các phí khác trong công cụ bên dưới.");
        if (item.Cost.Unknown.Count > 0)
            lines.Add("Khoản cần làm rõ:\n" + string.Join('\n', item.Cost.Unknown.Select(unknown => "- " + unknown)));
        lines.Add("Thông tin từ tin đăng; bạn có thể soạn câu hỏi cho chủ tin để xác nhận các khoản còn thiếu.");
        return string.Join("\n\n", lines);
    }

    private static string Money(decimal amount) => amount.ToString("#,##0.##", Vietnamese);

    [GeneratedRegex(@"\b(?:(?:tim|loc|goi y)(?: giup (?:toi|minh))?\s+(?:phong|tro)|(?:phong|tro) khac|tim lai|bat dau lai)\b", RegexOptions.CultureInvariant)]
    private static partial Regex NewSearch();

    [GeneratedRegex(@"\b(?:(?:hoi|nhan|soan)(?: (?:tin|cau hoi|tin nhan))?(?: cho)? (?:chu|nguoi cho thue)|(?:can|nen) hoi)\b", RegexOptions.CultureInvariant)]
    private static partial Regex OwnerQuestions();

    [GeneratedRegex(@"\b(?:tien thang|tien thue|dien(?!\s+thoai)|nuoc(?!\s+(?:ngoai|uong))|coc|chi phi|tong tien|ca phi)\b", RegexOptions.CultureInvariant)]
    private static partial Regex CostQuestion();

    [GeneratedRegex(@"\b(?:(?:phong|tin) (?:nay|dang xem)|phong duoc chon)\b", RegexOptions.CultureInvariant)]
    private static partial Regex SelectedListing();
}
