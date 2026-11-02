using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Homeji.Application.Services.RentalPosts;

internal sealed record RentalSourceFactsResult(string? DepositLabel, IReadOnlyList<string> Amenities);

internal static class RentalSourceFacts
{
    private static readonly Regex DepositPattern = new(
        @"(?:tiền\s*)?(?:đặt\s*)?cọc\s*(?:là\s*)?[:\-]?\s*(?<value>\d+(?:[.,]\d+)?\s*(?:tỷ|tỉ|triệu|tr|tháng|đồng|đ|vnd)?)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly (string[] Terms, string Label)[] AmenityTerms =
    [
        (["may lanh", "dieu hoa"], "Máy lạnh"),
        (["may nuoc nong", "nuoc nong"], "Máy nước nóng"),
        (["tu lanh"], "Tủ lạnh"),
        (["may giat"], "Máy giặt"),
        (["wifi", "internet"], "Wi-Fi"),
        (["ban cong"], "Ban công"),
        (["thang may"], "Thang máy"),
        (["gac lung", "co gac"], "Gác lửng"),
        (["nau an", "bep rieng"], "Bếp"),
    ];

    public static RentalSourceFactsResult Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return new(null, []);

        var normalized = Normalize(description);
        var depositMatch = DepositPattern.Match(description);
        var deposit = depositMatch.Success ? $"Cọc {depositMatch.Groups["value"].Value.Trim()}" : null;
        var amenities = AmenityTerms
            .Where(item => item.Terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            .Select(item => item.Label)
            .Distinct(StringComparer.Ordinal)
            .Take(6)
            .ToArray();

        return new(deposit, amenities);
    }

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
