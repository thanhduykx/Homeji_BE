using System.Text.RegularExpressions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.AI;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Chatbot;

/// <summary>Only recent user instructions can resume a posting task. Assistant
/// prose is never evidence that a draft or published post actually exists.</summary>
public static class ChatbotPostingContext
{
    public static bool IsConfirmationOnly(string message) => Regex.IsMatch(RentalSearchIntent.Normalize(message).Trim(),
        @"^(?:1|ok(?:ay)?|dong y|xac nhan|dang di|gui di|duoc|u|uh|vang|co)(?:[.!\s]*)$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static bool IsRefinement(string message)
    {
        var text = RentalSearchIntent.Normalize(message);
        return IsConfirmationOnly(message) || Regex.IsMatch(text,
            @"^(?:\d+[.)]\s*)?(?:(?:tinh trang|ki(?:ch|ck) thuoc|chat lieu|moi|da su dung|so luong|dia chi|vi tri|sua|doi|them|bo sung|cap nhat|chinh|tieu de|mo ta|dung vi tri|bang da)\b|gia(?: ban)?\s*(?:la|con|:)?\s*\d)|^\d+(?:[.,]\d+)?\s*(?:k|tr|trieu|dong|₫)\s*$",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    }

    public static string? Resolve(string message, IEnumerable<ChatbotMessageDto> recentMessages, bool hasDraft)
    {
        if (AgentBusinessIntent.PostingKind(message) is not null) return message;
        if (AgentBusinessIntent.IsDiscovery(message)) return null;
        if (hasDraft && !Regex.IsMatch(RentalSearchIntent.Normalize(message), @"\b(?:premium|goi thanh vien|mat khau|tai khoan)\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) return message;
        if (!IsRefinement(message)) return null;
        var users = ChatbotSessionContext.Bound(recentMessages).Where(row => row.Sender == ChatMessageSender.User).ToArray();
        var additions = new List<string>();
        foreach (var prior in users.Reverse().Take(3))
        {
            if (AgentBusinessIntent.PostingKind(prior.Content) is not null)
                return prior.Content + "\nThông tin bổ sung của người dùng (ưu tiên dòng mới nhất): " + string.Join("\n", additions.AsEnumerable().Reverse().Append(message));
            if (!IsRefinement(prior.Content)) break;
            additions.Add(prior.Content);
        }
        return null;
    }
}
