using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Homeji.Application.DTOs.AI;

namespace Homeji.Application.Services.AI;

/// <summary>Bounded, deterministic edits to explicit preferences. Never infers missing fees.</summary>
public static partial class RentalSearchIntent
{
    public static readonly IReadOnlyDictionary<string, string[]> AmenityAliases = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["KITCHEN"] = ["bep", "nau an"],
        ["AIR_CONDITIONER"] = ["may lanh", "dieu hoa"],
        ["WIFI"] = ["wifi", "internet"],
        ["PARKING"] = ["giu xe", "bai xe"],
        ["PRIVATE_BATHROOM"] = ["wc rieng", "toilet rieng", "ve sinh rieng"],
        ["PET_FRIENDLY"] = ["thu cung", "nuoi meo", "nuoi cho"],
        ["FREE_TIME"] = ["gio giac tu do"],
    };

    public static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character is 'đ' or 'Đ' ? 'd' : char.ToLowerInvariant(character));
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool IsRentalQuery(string message) => RentalTerms().IsMatch(Normalize(message));

    public static AiParsedSearchCriteriaDto Apply(string message, AiParsedSearchCriteriaDto? previous = null)
    {
        var text = Normalize(message);
        var state = previous ?? new AiParsedSearchCriteriaDto(null, null, null, null, null, null, []);
        if (text.Contains("tim lai", StringComparison.Ordinal) || text.Contains("bat dau lai", StringComparison.Ordinal))
            state = new AiParsedSearchCriteriaDto(null, null, null, null, null, null, []);
        var required = state.RequiredAmenities.ToHashSet(StringComparer.Ordinal);
        var excluded = state.ExcludedAmenities.ToHashSet(StringComparer.Ordinal);
        var optional = state.Criteria.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = state.Unknown.ToHashSet(StringComparer.Ordinal);
        foreach (var (code, aliases) in AmenityAliases)
        {
            foreach (var alias in aliases)
            {
                var match = Regex.Match(text, $@"\b(?<prefix>(?:khong can|bo|khong co|khong muon|uu tien|mong muon|co|can|bat buoc)\s+)?{Regex.Escape(alias)}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                if (!match.Success) continue;
                var prefix = match.Groups["prefix"].Value.Trim();
                required.Remove(code); excluded.Remove(code); optional.Remove(code);
                if (prefix is "khong can" or "bo") continue;
                if (prefix is "khong co" or "khong muon") excluded.Add(code);
                else if (prefix is "uu tien" or "mong muon") optional.Add(code);
                else required.Add(code);
                break;
            }
        }
        var range = BudgetRange().Match(text);
        if (range.Success)
        {
            var unit = range.Groups["unit"].Value;
            var minUnit = range.Groups["minUnit"].Success ? range.Groups["minUnit"].Value : unit;
            if (TryMoney(range.Groups["min"].Value, minUnit, out var minimum) && TryMoney(range.Groups["max"].Value, unit, out var maximum))
            {
                state = state with { PriceMin = minimum, PriceMax = maximum };
                unknown.Remove("budget");
            }
            else unknown.Add("budget");
        }
        var budget = Money().Match(text);
        if (!range.Success && budget.Success && TryMoney(budget.Groups["amount"].Value, budget.Groups["unit"].Value, out var amount))
        {
            if (amount is > 0 and <= 1_000_000_000)
            {
                state = budget.Groups["bound"].Value is "tu" or "tren" ? state with { PriceMin = amount } : state with { PriceMax = amount };
                unknown.Remove("budget");
            }
        }
        else if (!range.Success && (budget.Success || text.Contains("re hon", StringComparison.Ordinal))) unknown.Add("budget");
        if (text.Contains("bo ngan sach", StringComparison.Ordinal)) { state = state with { PriceMin = null, PriceMax = null }; unknown.Remove("budget"); }
        var occupants = People().Match(text);
        if (occupants.Success)
        {
            if (int.TryParse(occupants.Groups[1].Value, out var count) && count is >= 1 and <= 20)
            { state = state with { Occupants = count }; unknown.Remove("occupants"); }
            else unknown.Add("occupants");
        }
        if (Regex.IsMatch(text, @"\bkhong(?: muon| can)? o ghep\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))) state = state with { ExcludeRoommateShare = true };
        else if (text.Contains("chap nhan o ghep", StringComparison.Ordinal)) state = state with { ExcludeRoommateShare = false };
        if (text.Contains("ca phi", StringComparison.Ordinal) || text.Contains("tong chi phi", StringComparison.Ordinal)) state = state with { BudgetBasis = "total" };
        else if (text.Contains("chi tien thue", StringComparison.Ordinal)) state = state with { BudgetBasis = "rent" };
        if (state.BudgetBasis == "total") unknown.Add("feeUnits"); else unknown.Remove("feeUnits");
        if (text.Contains("thu duc", StringComparison.Ordinal)) state = state with { Location = "Thủ Đức" };
        else if (text.Contains("quan 9", StringComparison.Ordinal)) state = state with { Location = "Quận 9" };
        if (text.Contains("fpt", StringComparison.Ordinal)) { state = state with { Destination = "FPT" }; unknown.Add("destination"); }
        var schools = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Sư phạm Kỹ thuật"] = ["spkt", "hcmute", "su pham ky thuat"],
            ["Công nghệ Thông tin"] = ["uit", "cong nghe thong tin"],
            ["Đại học Quốc tế"] = ["dai hoc quoc te"],
            ["Bách khoa"] = ["bach khoa", "hcmut"],
            ["Khoa học Tự nhiên"] = ["khoa hoc tu nhien", "hcmus"],
            ["Kinh tế – Luật"] = ["kinh te luat", "uel"],
            ["Nông Lâm"] = ["nong lam"],
            ["Ngân hàng"] = ["dai hoc ngan hang"],
            ["HUTECH"] = ["hutech"],
            ["Văn Lang"] = ["van lang"],
            ["Nguyễn Tất Thành"] = ["nguyen tat thanh"],
        };
        foreach (var (school, aliases) in schools)
            if (aliases.Any(alias => Regex.IsMatch(text, $@"\b{Regex.Escape(alias)}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))))
            { state = state with { Destination = school }; unknown.Add("destination"); break; }
        if (text.Contains("khu cong nghe cao", StringComparison.Ordinal) && state.Destination == "FPT")
        { state = state with { Destination = "FPT Khu Công nghệ cao" }; unknown.Remove("destination"); }
        if (text.Contains("bo diem den", StringComparison.Ordinal)) { state = state with { Destination = null }; unknown.Remove("destination"); }
        var commute = CommuteLimit().Match(text);
        if (commute.Success && int.TryParse(commute.Groups[1].Value, out var minutes))
        {
            if (minutes is >= 1 and <= 240) state = state with { MaxCommuteMinutes = minutes };
            else unknown.Add("commute");
        }
        if (text.Contains("di bo", StringComparison.Ordinal)) state = state with { TravelMode = "WALKING" };
        else if (text.Contains("xe buyt", StringComparison.Ordinal) || text.Contains("phuong tien cong cong", StringComparison.Ordinal)) state = state with { TravelMode = "TRANSIT" };
        else if (text.Contains("o to", StringComparison.Ordinal)) state = state with { TravelMode = "DRIVING" };
        else if (text.Contains("xe may", StringComparison.Ordinal)) { state = state with { TravelMode = null }; unknown.Add("motorcycleCoverage"); }
        if (state.TravelMode is not null) unknown.Remove("motorcycleCoverage");
        if (text.Contains("bo diem den", StringComparison.Ordinal)) { state = state with { MaxCommuteMinutes = null, TravelMode = null }; unknown.Remove("motorcycleCoverage"); }
        // Naming a campus does not prove proximity. Routes must be explicitly calculated by the user.
        if (state.Destination is not null || state.MaxCommuteMinutes.HasValue || commute.Success || unknown.Contains("motorcycleCoverage")) unknown.Add("commute"); else unknown.Remove("commute");
        if (state.MaxCommuteMinutes.HasValue && state.Destination is null) unknown.Add("destination");
        if (state.PriceMin > state.PriceMax) unknown.Add("priceRange"); else unknown.Remove("priceRange");
        var areaRange = AreaRange().Match(text);
        var area = Area().Match(text);
        if (areaRange.Success)
        {
            if (TryArea(areaRange.Groups["min"].Value, out var minArea) && TryArea(areaRange.Groups["max"].Value, out var maxArea) && minArea <= maxArea)
            { state = state with { AreaMin = minArea, AreaMax = maxArea }; unknown.Remove("area"); }
            else unknown.Add("area");
        }
        else if (area.Success)
        {
            if (TryArea(area.Groups["amount"].Value, out var areaValue))
            {
                state = area.Groups["bound"].Value is "duoi" or "toi da" or "khong qua"
                    ? state with { AreaMax = areaValue } : state with { AreaMin = areaValue };
                unknown.Remove("area");
            }
            else unknown.Add("area");
        }
        if (text.Contains("bo dien tich", StringComparison.Ordinal)) { state = state with { AreaMin = null, AreaMax = null }; unknown.Remove("area"); }
        if (state.AreaMin > state.AreaMax) unknown.Add("area");
        return state with { RequiredAmenities = required.Order(StringComparer.Ordinal).ToArray(), ExcludedAmenities = excluded.Order(StringComparer.Ordinal).ToArray(), Criteria = optional.Order(StringComparer.Ordinal).ToArray(), Unknown = unknown.Order(StringComparer.Ordinal).ToArray() };
    }

    private static bool TryMoney(string text, string unit, out decimal amount)
    {
        var currency = unit is "vnd" or "dong" or "d";
        var normalized = currency && Regex.IsMatch(text, @"^\d{1,3}(?:[.,]\d{3})+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) ? text.Replace(",", "", StringComparison.Ordinal).Replace(".", "", StringComparison.Ordinal) : text.Replace(',', '.');
        var valid = decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount);
        amount *= unit is "tr" or "trieu" ? 1_000_000 : unit is "k" or "ngan" or "nghin" ? 1_000 : 1;
        return valid && amount is > 0 and <= 1_000_000_000;
    }

    private static bool TryArea(string text, out decimal area) => decimal.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out area) && area is > 0 and <= 100_000;

    [GeneratedRegex(@"(?<bound>tu|tren|it nhat|toi thieu|duoi|toi da|khong qua)?\s*(?<amount>\d+(?:[.,]\d+)?)\s*(?:m2|m²|met vuong)(?=$|[\s,.;!?])", RegexOptions.CultureInvariant)]
    private static partial Regex Area();
    [GeneratedRegex(@"(?:tu\s+)?(?<min>\d+(?:[.,]\d+)?)\s*(?:m2|m²|met vuong)?\s*(?:den|toi|-)\s*(?<max>\d+(?:[.,]\d+)?)\s*(?:m2|m²|met vuong)(?=$|[\s,.;!?])", RegexOptions.CultureInvariant)]
    private static partial Regex AreaRange();

    [GeneratedRegex(@"\b(phong|tro|thue|o ghep|ngan sach|gia|gan|bep|may lanh|re hon|nguoi|ca phi|diem den|tien thue|wifi|internet|thu cung|giu xe|wc rieng|ve sinh rieng|fpt|khu cong nghe cao|phut|di bo|o to|xe buyt|xe may|spkt|hcmute|uit|hcmut|hcmus|uel|hutech|dai hoc|truong|dien tich)\b", RegexOptions.CultureInvariant)]
    private static partial Regex RentalTerms();
    [GeneratedRegex(@"(?<bound>duoi|toi da|khong qua|cao nhat|tu|tren)?\s*(?<amount>\d+(?:[.,]\d+)*)\s*(?<unit>trieu|tr|vnd|dong|nghin|ngan|k|d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex Money();
    [GeneratedRegex(@"(?:tu\s+)?(?<min>\d+(?:[.,]\d+)*)\s*(?<minUnit>trieu|tr|vnd|dong|nghin|ngan|k|d)?\s*(?:den|toi|-)\s*(?<max>\d+(?:[.,]\d+)*)\s*(?<unit>trieu|tr|vnd|dong|nghin|ngan|k|d)\b", RegexOptions.CultureInvariant)]
    private static partial Regex BudgetRange();
    [GeneratedRegex(@"(?:duoi|toi da|khong qua|trong|<=|≤)\s*(\d{1,3})\s*phut\b", RegexOptions.CultureInvariant)]
    private static partial Regex CommuteLimit();
    [GeneratedRegex(@"\b(\d+)\s*nguoi\b", RegexOptions.CultureInvariant)]
    private static partial Regex People();
}
