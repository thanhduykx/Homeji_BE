using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.AI;
using System.Text.RegularExpressions;

namespace Homeji.Application.Services.Chatbot;

internal static partial class ChatbotRentalSelection
{
    public static string? ResolveTask(ChatbotRentalSelectionDto? selection, ChatbotRentalContextDto? room, string message)
    {
        if (selection is null) return null;
        if (room is not null || selection.PostIds is not { Count: >= 2 and <= 3 }
            || selection.PostIds.Contains(Guid.Empty) || selection.PostIds.Distinct().Count() != selection.PostIds.Count
            || selection.Task is not (null or "compare" or "cost" or "questions" or "details"))
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["rentalSelection"] = ["Chọn 2–3 tin khác nhau, một ngữ cảnh và tác vụ compare, cost, questions hoặc details."],
            });
        if (ChatbotRentalContext.StartsNewSearch(message)) return null;
        if (selection.Task is not null) return selection.Task;
        return SelectedChoices().IsMatch(RentalSearchIntent.Normalize(message))
            ? ChatbotRentalContext.InferDetailTask(message) ?? "compare" : null;
    }

    public static string BuildReply(string task, RentalDecisionResponseDto decision)
    {
        if (decision.Posts.Count == 0)
            return "Các lựa chọn này không còn khả dụng để so sánh. Hãy chọn lại tin công khai đang hoạt động. Mình chưa liên hệ hay đặt lịch với người đăng.";
        var lines = new List<string>();
        if (decision.UnavailablePostIds.Count > 0)
            lines.Add("Một số tin đã ẩn, hết hạn hoặc không còn trong phạm vi được đọc. Chỉ các tin còn khả dụng xuất hiện bên dưới; chưa thể coi đây là so sánh đủ mọi lựa chọn ban đầu.");
        lines.Add("Mình đang đối chiếu đúng các tin bạn đã chọn với cùng kịch bản chi phí.");
        foreach (var item in decision.Posts)
        {
            var single = new RentalDecisionResponseDto([item], [], "");
            lines.Add($"### {item.Post.Title}\n\n" + ChatbotRentalContext.BuildReply(task == "compare" ? "details" : task, single));
        }
        if (decision.Posts.Count > 1) lines.Add(decision.Summary);
        lines.Add("Xem bảng so sánh để chỉnh số người và các đơn vị phí. Mở chi tiết từng phòng để soạn câu hỏi hoặc yêu cầu lịch xem; chưa có tin nhắn hay lịch nào được gửi tự động.");
        return string.Join("\n\n", lines);
    }

    [GeneratedRegex(@"\b(?:(?:hai|ba|2|3|cac|nhung) (?:lua chon|phong|tin)(?: nay)?|so sanh(?: giup)?|danh sach (?:nay|can nhac))\b", RegexOptions.CultureInvariant)]
    private static partial Regex SelectedChoices();
}
