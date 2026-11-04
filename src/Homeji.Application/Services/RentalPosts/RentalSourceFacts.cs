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
    private static readonly (string[] Terms, string Label)[] AmenityTerms =
    [
        (["may lanh", "dieu hoa"], "Máy lạnh"),
        (["may nuoc nong", "nuoc nong"], "Máy nước nóng"),
        (["nau an", "bep rieng", "khu bep", "co bep", "bep nau"], "Bếp"),
        (["gio giac tu do", "gio tu do", "tu do gio giac", "khong gioi han gio", "di lai tu do", "ra vao tu do", "gio giac thoai mai"], "Giờ tự do"),
        (["tu lanh"], "Tủ lạnh"),
        (["may giat"], "Máy giặt"),
        (["wifi", "internet"], "Wi-Fi"),
        (["gac lung", "co gac"], "Gác lửng"),
        (["ban cong"], "Ban công"),
        (["thang may"], "Thang máy"),
    ];

    private static readonly string[] NoLandlordTerms = ["khong chung chu", "khong o chung chu", "chu o rieng", "chu khong o"];
    private static readonly string[] SharedLandlordTerms = ["chung chu", "o chung chu"];

    public static RentalSourceFactsResult Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return new(null, []);

        var normalized = Normalize(description);
        var depositMatch = DepositPattern.Match(description);
        var deposit = depositMatch.Success ? $"Cọc {depositMatch.Groups["value"].Value.Trim()}" : null;
        var amenities = AmenityTerms
            .Where(item => item.Terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            .Select(item => item.Label)
            .ToList();
        // "chung chủ" also matches inside "không chung chủ", so check the negated form first.
        var landlord = NoLandlordTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)) ? "Không chung chủ"
            : SharedLandlordTerms.Any(term => normalized.Contains(term, StringComparison.Ordinal)) ? "Chung chủ" : null;
        if (landlord is not null) amenities.Insert(Math.Min(4, amenities.Count), landlord);

        return new(deposit, amenities.Distinct(StringComparer.Ordinal).Take(6).ToArray());
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
