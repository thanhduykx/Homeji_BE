using System.Text.RegularExpressions;
using Homeji.Application.Services.AI;

namespace Homeji.Application.Services.Common;

/// <summary>Food discovery is routed before broad rental refinements such as “near” or “price”.</summary>
public static partial class FoodSearchIntent
{
    public static bool IsFoodTopic(string message)
    {
        var text = RentalSearchIntent.Normalize(message);
        var food = FoodTerms().Match(text);
        if (!food.Success) return false;
        var housing = HousingTerms().Match(text);
        return !housing.Success || food.Index < housing.Index;
    }

    public static bool IsDiscovery(string message) => IsFoodTopic(message)
        && !ProcedureTerms().IsMatch(RentalSearchIntent.Normalize(message));

    public static bool NeedsLocation(string message) => LocationTerms().IsMatch(RentalSearchIntent.Normalize(message));

    [GeneratedRegex(@"\b(do an|thuc an|mon an|quan an|co gi an|an gi|an uong|doi bung|dang doi|minh doi|tui doi|toi doi|com|bun|pho|banh mi|mi xao|tra sua|an sang|an trua|an toi)\b")]
    private static partial Regex FoodTerms();
    [GeneratedRegex(@"\b(phong|tro|can ho|o ghep|nha thue)\b")]
    private static partial Regex HousingTerms();
    [GeneratedRegex(@"\b(cach|huong dan|lam sao|nhu the nao|gio hang|don hang|thanh toan|huy don|hoan tien|muon dat do an|muon mua do an)\b")]
    private static partial Regex ProcedureTerms();
    [GeneratedRegex(@"\b(gan|quanh|khu vuc|quan \d|thu duc|o dau|dia chi|tai day|o day)\b")]
    private static partial Regex LocationTerms();
}
