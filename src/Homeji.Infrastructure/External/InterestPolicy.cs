using System.Globalization;
using System.Text;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
namespace Homeji.Infrastructure.External;

internal static class InterestPolicy
{
    public static string Key(string text) => string.Join(' ', new string(text.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
        .Select(c => char.IsLetterOrDigit(c) ? c : ' ').ToArray()).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    public static InterestCriteria Validate(InterestCriteria c)
    {
        if (string.IsNullOrWhiteSpace(c.Area) || c.Area.Trim().Length is < 2 or > 160 || Key(c.Area).Length < 2
            || c.MinPrice is < 0 or > 1000000000 || c.MaxPrice is < 0 or > 1000000000 || c.MinPrice > c.MaxPrice
            || c.MinArea is < 0 or > 10000 || c.MaxArea is < 0 or > 10000 || c.MinArea > c.MaxArea
            || c.MaxDeposit is < 0 or > 1000000000 || c.Occupants is < 1 or > 20
            || c.Amenities?.Length > 20 || c.ExcludedAmenities?.Length > 20
            || (c.Amenities ?? []).Concat(c.ExcludedAmenities ?? []).Any(a => string.IsNullOrWhiteSpace(a) || a.Length > 50)
            || new[] { c.Latitude, c.Longitude, c.RadiusKm }.Any(n => n.HasValue && !double.IsFinite(n.Value))
            || c.Latitude is < -90 or > 90 || c.Longitude is < -180 or > 180 || c.RadiusKm is < .5 or > 30
            || (c.Latitude.HasValue || c.Longitude.HasValue || c.RadiusKm.HasValue) && !(c.Latitude.HasValue && c.Longitude.HasValue && c.RadiusKm.HasValue))
            throw Invalid("criteria", "Khu vực hoặc bộ lọc không hợp lệ. Bán kính cần đủ tâm địa điểm và nằm trong 0,5–30 km.");
        var normalized = c with { Area = c.Area.Trim(), Amenities = (c.Amenities ?? []).Select(a => a.Trim().ToUpperInvariant()).Distinct().ToArray(), ExcludedAmenities = (c.ExcludedAmenities ?? []).Select(a => a.Trim().ToUpperInvariant()).Distinct().ToArray() };
        if (normalized.Amenities!.Intersect(normalized.ExcludedAmenities!).Any())
            throw Invalid("amenities", "Một tiện ích không thể vừa bắt buộc vừa bị loại trừ.");
        return normalized;
    }
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double r = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * r / 2), 2) + Math.Cos(lat1 * r) * Math.Cos(lat2 * r) * Math.Pow(Math.Sin((lng2 - lng1) * r / 2), 2);
        return 6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
    public static RequestValidationException Invalid(string key, string message) => new(new Dictionary<string, string[]> { [key] = [message] });
}
