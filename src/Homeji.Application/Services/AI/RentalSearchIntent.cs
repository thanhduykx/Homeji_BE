using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Homeji.Application.DTOs.AI;

namespace Homeji.Application.Services.AI;

/// <summary>Deterministic constraints and corrections, also used when the provider is unavailable.</summary>
public static partial class RentalSearchIntent
{
    public static readonly IReadOnlyDictionary<string, string[]> Amenities = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["PARKING"] = ["giu xe", "gui xe", "bai xe"],
        ["FREE_TIME"] = ["gio giac tu do", "tu do gio giac"],
        ["WIFI"] = ["wifi", "internet"],
        ["AIR_CONDITIONER"] = ["may lanh", "dieu hoa"],
        ["PRIVATE_TOILET"] = ["wc rieng", "ve sinh rieng", "toilet rieng"],
        ["PET_FRIENDLY"] = ["thu cung", "nuoi meo", "nuoi cho"],
        ["KITCHEN"] = ["bep", "nau an"],
        ["QUIET"] = ["yen tinh"],
        ["SECURITY"] = ["bao ve"],
    };

    public static string Normalize(string text)
    {
        var decomposed = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var result = new StringBuilder();
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(character);
        return result.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string AmenityCode(string value) => value switch
    {
        "parking" => "PARKING", "freeTime" => "FREE_TIME", "wifi" => "WIFI",
        "airConditioner" => "AIR_CONDITIONER", "privateToilet" => "PRIVATE_TOILET",
        "petFriendly" => "PET_FRIENDLY", "kitchen" => "KITCHEN", "quiet" => "QUIET", "security" => "SECURITY",
        _ => value.ToUpperInvariant(),
    };

    public static AiParsedSearchCriteriaDto Apply(string text, AiParsedSearchCriteriaDto? previous = null)
    {
        var value = Normalize(text);
        var state = value.Contains("tim lai", StringComparison.Ordinal) || value.Contains("xoa tieu chi", StringComparison.Ordinal)
            ? Empty() : previous ?? Empty();
        var required = state.RequiredAmenities.Select(AmenityCode).ToHashSet(StringComparer.Ordinal);
        var excluded = state.ExcludedAmenities.Select(AmenityCode).ToHashSet(StringComparer.Ordinal);
        var preferred = state.Criteria.Select(AmenityCode).ToHashSet(StringComparer.Ordinal);
        foreach (var (code, aliases) in Amenities)
        {
            foreach (var alias in aliases)
            {
                var pattern = Regex.Escape(alias);
                if (!Regex.IsMatch(value, $@"\b{pattern}\b", RegexOptions.CultureInvariant)) continue;
                if (Regex.IsMatch(value, $@"(?:khong can|bo|it quan tam|khong bat buoc)\s+(?:co\s+)?{pattern}", RegexOptions.CultureInvariant))
                { required.Remove(code); preferred.Remove(code); excluded.Remove(code); }
                else if (Regex.IsMatch(value, $@"(?:khong co|khong muon|khong thich)\s+{pattern}", RegexOptions.CultureInvariant))
                { required.Remove(code); preferred.Remove(code); excluded.Add(code); }
                else if (Regex.IsMatch(value, $@"(?:uu tien|mong muon|neu co)\s+(?:co\s+)?{pattern}", RegexOptions.CultureInvariant))
                { required.Remove(code); excluded.Remove(code); preferred.Add(code); }
                else { excluded.Remove(code); preferred.Remove(code); required.Add(code); }
                break;
            }
        }
        var amounts = MoneyPattern().Matches(value);
        var money = amounts.Count > 0 ? amounts[0] : Match.Empty;
        if (money.Success)
        {
            var amount = decimal.Parse(money.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) * 1_000_000;
            var prefix = value[..money.Index];
            state = prefix.EndsWith("tu ", StringComparison.Ordinal) || prefix.EndsWith("toi thieu ", StringComparison.Ordinal)
                ? state with { PriceMin = amount } : state with { PriceMax = amount };
            if (amounts.Count >= 2)
            {
                var between = value[(money.Index + money.Length)..amounts[1].Index];
                if (between.Contains("den", StringComparison.Ordinal) || between.Contains('-', StringComparison.Ordinal))
                    state = state with { PriceMin = amount, PriceMax = decimal.Parse(amounts[1].Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture) * 1_000_000 };
            }
        }
        var vnd = VndPattern().Match(value);
        if (!money.Success && vnd.Success)
            state = state with { PriceMax = decimal.Parse(vnd.Groups[1].Value.Replace(".", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal), CultureInfo.InvariantCulture) };
        if (value.Contains("re hon", StringComparison.Ordinal) && !money.Success && state.PriceMax is > 0)
            state = state with { PriceMax = Math.Floor(state.PriceMax.Value * 0.9m) };
        if (value.Contains("bo ngan sach", StringComparison.Ordinal)) state = state with { PriceMin = null, PriceMax = null };
        if (value.Contains("ca phi", StringComparison.Ordinal) || value.Contains("tong chi phi", StringComparison.Ordinal)) state = state with { BudgetKind = "total" };
        if (value.Contains("chi tien thue", StringComparison.Ordinal)) state = state with { BudgetKind = "rent" };
        var occupants = OccupantsPattern().Match(value);
        if (occupants.Success) state = state with { Occupants = int.Parse(occupants.Groups[1].Value, CultureInfo.InvariantCulture) };
        if (value.Contains("khong o ghep", StringComparison.Ordinal)) state = state with { ExcludeShared = true };
        if (value.Contains("chap nhan o ghep", StringComparison.Ordinal)) state = state with { ExcludeShared = false };
        if (value.Contains("bo so nguoi", StringComparison.Ordinal)) state = state with { Occupants = null };
        if (value.Contains("bo dien tich", StringComparison.Ordinal)) state = state with { AreaMin = null, AreaMax = null };
        if (value.Contains("bo khu vuc", StringComparison.Ordinal)) state = state with { Location = null, Keyword = null };
        if (value.Contains("bo diem den", StringComparison.Ordinal)) state = state with { Destination = null };
        if (value.Contains("bo thoi gian di", StringComparison.Ordinal)) state = state with { MaxCommuteMinutes = null };
        var area = AreaPattern().Match(value);
        if (area.Success)
        {
            var amount = decimal.Parse(area.Groups[2].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
            state = area.Groups[1].Value is "duoi" or "toi da" ? state with { AreaMax = amount } : state with { AreaMin = amount };
        }
        var commute = CommutePattern().Match(value);
        if (commute.Success) state = state with { MaxCommuteMinutes = int.Parse(commute.Groups[1].Value, CultureInfo.InvariantCulture) };
        foreach (var destination in new[] { "fpt", "hutech", "spkt", "su pham ky thuat", "dhqg", "nha van hoa sinh vien" })
            if (value.Contains(destination, StringComparison.Ordinal)) state = state with { Destination = destination, Location = null, Keyword = null };
        foreach (var location in new[] { "thu duc", "quan 9", "tang nhon phu", "long thanh my", "long truong", "hiep phu", "phuoc long", "linh trung", "linh xuan" })
            if (value.Contains(location, StringComparison.Ordinal)) state = state with { Location = location, Keyword = null };
        var unknown = new List<string>();
        if (state.Destination is not null) unknown.Add("Chọn chính xác cơ sở trường/điểm đến để tính đường đi; tên trường không chứng minh phòng ở gần.");
        if (state.MaxCommuteMinutes.HasValue) unknown.Add("Chọn điểm đến và phương tiện, sau đó tính tuyến đường cho danh sách ngắn.");
        if (state.BudgetKind == "total") unknown.Add("Cần xác nhận đơn vị, các khoản phí và mức sử dụng trước khi kiểm tra tổng ngân sách.");
        return state with { RequiredAmenities = required.ToArray(), ExcludedAmenities = excluded.ToArray(), Criteria = preferred.ToArray(), Unknown = unknown };
    }

    public static AiParsedSearchCriteriaDto Empty() => new(null, null, null, null, null, null, []);

    public static AiParsedSearchCriteriaDto Merge(AiParsedSearchCriteriaDto state, AiParsedSearchCriteriaDto update) => state with
    {
        Location = update.Location ?? state.Location,
        Keyword = update.Keyword ?? state.Keyword,
        PriceMin = update.PriceMin ?? state.PriceMin,
        PriceMax = update.PriceMax ?? state.PriceMax,
        AreaMin = update.AreaMin ?? state.AreaMin,
        AreaMax = update.AreaMax ?? state.AreaMax,
        Occupants = update.Occupants ?? state.Occupants,
        Destination = update.Destination ?? state.Destination,
        MaxCommuteMinutes = update.MaxCommuteMinutes ?? state.MaxCommuteMinutes,
        BudgetKind = update.BudgetKind == "total" ? "total" : state.BudgetKind,
        ExcludeShared = update.ExcludeShared || state.ExcludeShared,
        RequiredAmenities = state.RequiredAmenities.Concat(update.RequiredAmenities).Distinct(StringComparer.Ordinal).ToArray(),
        ExcludedAmenities = state.ExcludedAmenities.Concat(update.ExcludedAmenities).Distinct(StringComparer.Ordinal).ToArray(),
        Criteria = state.Criteria.Concat(update.Criteria).Distinct(StringComparer.Ordinal).ToArray(),
    };

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(?:trieu|tr|triệu)\b", RegexOptions.CultureInvariant)]
    private static partial Regex MoneyPattern();
    [GeneratedRegex(@"\b(\d{1,3}(?:[.,]\d{3}){2,3}|\d{6,9})\s*(?:dong|vnd)?\b", RegexOptions.CultureInvariant)]
    private static partial Regex VndPattern();
    [GeneratedRegex(@"(\d+)\s*nguoi\b", RegexOptions.CultureInvariant)]
    private static partial Regex OccupantsPattern();
    [GeneratedRegex(@"(?:duoi|toi da|khong qua)\s*(\d+)\s*phut", RegexOptions.CultureInvariant)]
    private static partial Regex CommutePattern();
    [GeneratedRegex(@"(?:(duoi|toi da|tu|toi thieu|tren)\s*)?(\d+(?:[.,]\d+)?)\s*(?:m2|m²|met vuong)\b", RegexOptions.CultureInvariant)]
    private static partial Regex AreaPattern();
}
