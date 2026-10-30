using System.Text.Json;
using Homeji.Application.DTOs.Moderation;

namespace Homeji.Application.Services.Moderation;

public static class AiListingReviewPolicy
{
    private static readonly string[] Negations = ["không", "cấm", "chống", "cảnh báo"];
    public static bool CanAutoBlock(AiReviewVerdict verdict, string snapshot)
    {
        if (verdict.Decision != "block" || verdict.Confidence < 0.98 || string.IsNullOrWhiteSpace(verdict.Evidence)
            || verdict.Category is not ("illegal_drugs" or "sexual_exploitation" or "explicit_threat")) return false;
        // Only direct high-risk phrases are eligible for automatic action. Inferences stay with a human reviewer.
        var phrases = verdict.Category switch {
            "illegal_drugs" => new[] { "bán ma túy", "bán heroin", "bán cần sa" },
            "sexual_exploitation" => new[] { "mua bán trẻ em", "bán dâm trẻ em" },
            _ => new[] { "sẽ giết", "giết cả nhà" },
        };
        if (!phrases.Any(phrase => verdict.Evidence.Contains(phrase, StringComparison.OrdinalIgnoreCase))) return false;
        if (Negations.Any(word => verdict.Evidence.Contains(word, StringComparison.OrdinalIgnoreCase))) return false;
        using var document = JsonDocument.Parse(snapshot);
        return document.RootElement.EnumerateObject().Where(field => field.Value.ValueKind == JsonValueKind.String)
            .Any(field => {
                var text = field.Value.GetString()!;
                var index = text.IndexOf(verdict.Evidence, StringComparison.OrdinalIgnoreCase);
                if (index < 0) return false;
                var context = text.Substring(Math.Max(0, index - 80), Math.Min(text.Length, index + verdict.Evidence.Length + 80) - Math.Max(0, index - 80));
                return !Negations.Any(word => context.Contains(word, StringComparison.OrdinalIgnoreCase));
            });
    }
}
