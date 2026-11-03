using Homeji.Application.DTOs.Chatbot;
using Homeji.Domain.Enums;
using Homeji.Application.Services.AI;

namespace Homeji.Application.Services.Chatbot;

public static class ChatbotSessionContext
{
    public static bool RequestsNearbyRooms(string message) => RentalSearchIntent.IsRentalQuery(message)
        && System.Text.RegularExpressions.Regex.IsMatch(RentalSearchIntent.Normalize(message), @"\b(?:gan (?:toi|minh|tui|day)|quanh (?:toi|minh|tui|day)|vi tri cua (?:toi|minh|tui))\b",
            System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100));
    private static readonly string[] PostingPhrases = ["soan bai", "tao tin", "viet bai", "giup toi cho thue", "dang bai", "dang ban", "dang tin", "post len", "tu post", "tu dang", "ban giup", "pass mon", "pass do", "len bai", "tao bai", "rao ban", "dang len"];
    public static IReadOnlyList<ChatbotMessageDto> Bound(IEnumerable<ChatbotMessageDto>? messages)
        => (messages ?? []).TakeLast(6).Where(row => row is not null && row.Sender is ChatMessageSender.User or ChatMessageSender.Assistant)
            .Select(row => row with { Content = (row.Content ?? "")[..Math.Min(row.Content?.Length ?? 0, 1000)] }).ToArray();

    public static bool RequestsPosting(string message)
    {
        var text = NormalizePostingLanguage(message);
        if (text.StartsWith("co ai ", StringComparison.Ordinal) || text.StartsWith("ai dang ", StringComparison.Ordinal)
            || text.StartsWith("cach ", StringComparison.Ordinal) || text.StartsWith("huong dan ", StringComparison.Ordinal)) return false;
        return System.Text.RegularExpressions.Regex.IsMatch(message.Normalize(), @"\bđăng\s+(?:tìm|tuyển)\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            || PostingPhrases.Any(text.Contains)
            || System.Text.RegularExpressions.Regex.IsMatch(text, @"\b(?:dang pass|pass (?:lai|ghe|sofa|tu|giuong|ban|xe)|thanh ly|rao ban|dang ho|dang giup)\b|\b(?:toi|minh|em|anh|chi)\s+muon\s+(?:ban|pass)\b",
                System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    public static string NormalizePostingLanguage(string message) => System.Text.RegularExpressions.Regex.Replace(
        RentalSearchIntent.Normalize(message), @"\b(?:pas|passs)\b", "pass",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
}
