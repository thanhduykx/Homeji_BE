using System.Text.RegularExpressions;
using Homeji.Application.Services.AI;
namespace Homeji.Application.Services.Chatbot;

public static class AgentBusinessIntent
{
    public static string? PostingKind(string message)
    {
        var text = RentalSearchIntent.Normalize(message);
        if (Regex.IsMatch(text, @"^(?:co ai|ai dang|cach |huong dan )", RegexOptions.None, TimeSpan.FromMilliseconds(100))) return null;
        if (text.StartsWith("ban kinh", StringComparison.Ordinal)) return null;
        var posting = Regex.IsMatch(text, @"^(?:ban |pass |cho thue )", RegexOptions.None, TimeSpan.FromMilliseconds(100)) || ChatbotSessionContext.RequestsPosting(message)
            || Has(text, "dang cho thue", "dang ghe", "dang sofa", "dang mon", "dang do", "dang phong", "toi muon cho thue", "minh muon cho thue", "can pass phong", "muon pass phong", "can sang nhuong phong", "ban giup", "toi ban", "minh ban");
        if (!posting) return null;
        if (Has(text, "pass phong", "chuyen nhuong phong", "sang nhuong phong", "chuyen hop dong", "cho thue lai")) return "roomTransfer";
        if (Has(text, "tim tro", "tim phong", "nhu cau thue", "can thue")) return "wanted";
        if (Has(text, "o ghep", "ban cung phong", "roommate")) return "roommate";
        if (Has(text, "cho thue", "dang phong", "dang tin phong", "dang can ho", "dang tin can ho")) return "rental";
        if (Has(text, "do an", "mon an", "com", "bun", "pho", "tra sua", "banh", "thuc an", "thuc uong", "ca phe", "nuoc ep")) return "food";
        return "secondHand";
    }
    public static bool IsDiscovery(string message)
    {
        var text = RentalSearchIntent.Normalize(message);
        if (Regex.IsMatch(text, @"^(?:sua |doi |cap nhat |them |bo sung |chinh )", RegexOptions.None, TimeSpan.FromMilliseconds(100))) return false;
        return Regex.IsMatch(text, @"\b(?:tim |goi y|so sanh|toi muon mua|minh muon mua|co phong|co mon|mon nao|an gi)\b", RegexOptions.None, TimeSpan.FromMilliseconds(100))
            || Homeji.Application.Services.Common.FoodSearchIntent.IsDiscovery(message);
    }
    public static bool IsRoommateSearch(string message) => PostingKind(message) is null
        && Has(RentalSearchIntent.Normalize(message), "tim ban o ghep", "tim nguoi o ghep", "tim ban cung phong", "tim roommate");
    private static bool Has(string text, params string[] values) => values.Any(value => Regex.IsMatch(text,
        @"(?<![a-z0-9])" + Regex.Escape(value) + @"(?![a-z0-9])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)));
}
