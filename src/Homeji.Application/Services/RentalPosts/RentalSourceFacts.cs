using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Homeji.Application.Services.RentalPosts;

internal sealed record RentalSourceFactsResult(string? DepositLabel, IReadOnlyList<string> Amenities);

internal static class RentalSourceFacts
{
    private static readonly Regex ContactPhonePattern = new(
        @"(?<!\d)(?:0(?:[\s().-]*\d){9,10}|\+?84(?:[\s().-]*\d){9})(?!\d)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DepositPattern = new(
        @"(?:tiền\s*)?(?:đặt\s*)?cọc\s*(?:là\s*)?[:\-]?\s*(?<value>\d+(?:[.,]\d+)?\s*(?:tỷ|tỉ|triệu|tr|tháng|đồng|đ|vnd)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    // Ordered by what students scan for first; the card shows the first few.
    // Code matches RentalSearchIntent.AmenityAliases so chat filters can use the same evidence.
    private static readonly (string[] Terms, string Label, string? Code)[] AmenityTerms =
    [
        (["may lanh", "dieu hoa"], "Máy lạnh", "AIR_CONDITIONER"),
        (["may nuoc nong", "nuoc nong", "binh nong lanh"], "Máy nước nóng", "WATER_HEATER"),
        (["nau an", "bep rieng", "khu bep", "co bep", "bep nau"], "Bếp", "KITCHEN"),
        (["gio giac tu do", "gio tu do", "tu do gio giac", "khong gioi han gio", "di lai tu do", "ra vao tu do", "gio giac thoai mai"], "Giờ tự do", "FREE_TIME"),
        (["tu lanh"], "Tủ lạnh", null),
        (["may giat"], "Máy giặt", null),
        (["wifi", "internet"], "Wi-Fi", "WIFI"),
        (["wc rieng", "toilet rieng", "ve sinh rieng", "nha tam rieng"], "WC riêng", "PRIVATE_BATHROOM"),
        (["giu xe", "bai xe", "cho de xe", "ham xe"], "Chỗ để xe", "PARKING"),
        (["thu cung", "nuoi meo", "nuoi cho"], "Cho nuôi thú cưng", "PET_FRIENDLY"),
        (["gac lung", "co gac"], "Gác lửng", null),
        (["ban cong"], "Ban công", null),
        (["thang may"], "Thang máy", null),
    ];

    // "không nuôi thú cưng" and "cấm nuôi mèo" must not read as the amenity being offered.
    private static readonly Regex NegatedPrefix = new(
        @"\b(?:khong|cam|chua co)(?:\s+\S+){0,2}\s*$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly string[] NoLandlordTerms = ["khong chung chu", "khong o chung chu", "chu o rieng", "chu khong o"];
    private static readonly string[] SharedLandlordTerms = ["chung chu", "o chung chu"];

    public static RentalSourceFactsResult Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return new(null, []);

        var normalized = Normalize(description);
        var depositMatch = DepositPattern.Match(description);
        var deposit = depositMatch.Success ? $"Cọc {depositMatch.Groups["value"].Value.Trim()}" : null;
        var amenities = AmenityTerms
            .Where(item => item.Terms.Any(term => Mentions(normalized, term)))
            .Select(item => item.Label)
            .ToList();
        // "chung chủ" also matches inside "không chung chủ", so check the negated form first.
        var landlord = NoLandlordTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)) ? "Không chung chủ"
            : SharedLandlordTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)) ? "Chung chủ" : null;
        if (landlord is not null) amenities.Insert(Math.Min(4, amenities.Count), landlord);

        return new(deposit, amenities.Distinct(StringComparer.Ordinal).Take(6).ToArray());
    }

    public static IReadOnlySet<string> AmenityCodes(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return new HashSet<string>(StringComparer.Ordinal);
        var normalized = Normalize(description);
        return AmenityTerms
            .Where(item => item.Code is not null && item.Terms.Any(term => Mentions(normalized, term)))
            .Select(item => item.Code!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static bool Mentions(string normalized, string term)
    {
        for (var index = normalized.IndexOf(term, StringComparison.Ordinal); index >= 0;
            index = normalized.IndexOf(term, index + term.Length, StringComparison.Ordinal))
        {
            // Terms that already carry their own negation, such as "khong gioi han gio", start at the word itself.
            var before = normalized[Math.Max(0, index - 24)..index];
            if (!NegatedPrefix.IsMatch(before)) return true;
        }
        return false;
    }

    public static string? RemovePublicContactNumbers(string? description) =>
        string.IsNullOrWhiteSpace(description)
            ? description
            : ContactPhonePattern.Replace(description, "[số liên hệ ẩn]");

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        }

        return builder.ToString().Replace('đ', 'd').Replace('Đ', 'D').ToLowerInvariant();
    }
}
