using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.AI;

namespace Homeji.Application.Services.Chatbot;

/// <summary>Recognizes questions about rooms already listed and builds the facts Gemini may use to answer them.</summary>
internal static partial class ChatbotResultFollowUp
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public static bool IsFollowUp(string message, AiParsedSearchCriteriaDto previous)
    {
        if (ChatbotRentalContext.StartsNewSearch(message)) return false;
        var text = RentalSearchIntent.Normalize(message);
        if (PointsAtListedRoom().IsMatch(text)) return true;
        // "phòng nào rẻ nhất" asks about the list; "phòng nào dưới 2 triệu" narrows it, so it searches again.
        return AsksAboutList().IsMatch(text) && SameConstraints(RentalSearchIntent.Apply(message, previous), previous);
    }

    public static string BuildFacts(AiHighlightResponseDto? native, ChatbotSourceMatchDto? source)
    {
        var builder = new StringBuilder();
        var position = 0;
        foreach (var item in native?.Posts ?? [])
            builder.AppendLine(CultureInfo.InvariantCulture, $"Phòng {++position}: {item.Post.Title} | {Money(item.Post.Price)} | {Area(item.Post.Area)} | {item.Post.Address}");
        foreach (var room in source?.Rooms ?? [])
        {
            builder.Append(CultureInfo.InvariantCulture, $"Phòng {++position}: {room.Title} | {Money(room.Price)} | {Area(room.Area)} | {room.Address}");
            if (room.Amenities.Count > 0) builder.Append(CultureInfo.InvariantCulture, $" | Tiện ích trong mô tả: {string.Join(", ", room.Amenities)}");
            if (room.DepositLabel is not null) builder.Append(CultureInfo.InvariantCulture, $" | {room.DepositLabel}");
            if (room.Excerpt is not null) builder.Append(CultureInfo.InvariantCulture, $" | Mô tả: {room.Excerpt}");
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd();
    }

    private static string Money(decimal amount) => amount > 0 ? amount.ToString("#,##0", Vietnamese) + " đồng/tháng" : "chưa rõ giá";
    private static string Area(decimal area) => area > 0 ? area.ToString("0.##", Vietnamese) + " m²" : "chưa rõ diện tích";

    private static bool SameConstraints(AiParsedSearchCriteriaDto next, AiParsedSearchCriteriaDto previous) =>
        next.PriceMin == previous.PriceMin && next.PriceMax == previous.PriceMax
        && next.AreaMin == previous.AreaMin && next.AreaMax == previous.AreaMax
        && next.Occupants == previous.Occupants && next.Location == previous.Location && next.Destination == previous.Destination
        && next.RequiredAmenities.ToHashSet().SetEquals(previous.RequiredAmenities)
        && next.ExcludedAmenities.ToHashSet().SetEquals(previous.ExcludedAmenities)
        && next.Unknown.ToHashSet().SetEquals(previous.Unknown);

    // "phòng 2" points at a listed room; "phòng 2 người" and "phòng 3 triệu" are new constraints.
    [GeneratedRegex(@"\b(?:phong|tin|can)\s+(?:(?:thu|so)\s+)?\d{1,2}\b(?!\s*(?:nguoi|trieu|tr|m2|m²|k|ngan|nghin)\b)|\b(?:phong|tin|can)\s+(?:thu|so)\s+(?:nhat|hai|ba|bon|tu|nam)\b|\b(?:phong|tin|can)\s+(?:dau tien|cuoi cung)\b|\b\d{1,2}\s+(?:phong|tin|can)\s+dau\b|\b(?:cac|nhung|may)\s+(?:phong|tin|can)\s+(?:do|tren|nay|vua roi)\b", RegexOptions.CultureInvariant)]
    private static partial Regex PointsAtListedRoom();

    [GeneratedRegex(@"\b(?:re nhat|dat nhat|rong nhat|nho nhat|so sanh|nen chon|phong nao|tin nao|can nao)\b", RegexOptions.CultureInvariant)]
    private static partial Regex AsksAboutList();
}
