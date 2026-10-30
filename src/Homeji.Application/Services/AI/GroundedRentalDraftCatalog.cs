using System.Globalization;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;

namespace Homeji.Application.Services.AI;

/// <summary>The provider selects phrasing and order; only the server can introduce draft text.</summary>
internal sealed class GroundedRentalDraftCatalog
{
    private const int MaxProviderOutputLength = 4096;
    private readonly Dictionary<string, string> _titles;
    private readonly Dictionary<string, Dictionary<string, string>> _sections;

    public GroundedRentalDraftCatalog(string type, string? address, decimal? rent, decimal? area,
        string[] amenities, string priceBasis)
    {
        var rentText = rent.HasValue ? rent.Value.ToString("#,##0.############################", CultureInfo.GetCultureInfo("vi-VN")) + " đồng" : "chưa có thông tin hợp lệ";
        var unit = priceBasis switch { "person" => "/người/tháng", "room" => "/phòng/tháng", _ => "/tháng; chưa xác nhận giá theo phòng hay theo người" };
        if (rent.HasValue) rentText += unit;
        else if (priceBasis == "unknown") rentText += "; chưa xác nhận giá theo phòng hay theo người";
        var areaText = area.HasValue ? area.Value.ToString("0.############################", CultureInfo.GetCultureInfo("vi-VN")) + " m²" : "chưa có thông tin hợp lệ";
        var titleFacts = new List<string> { NormalizeSourceText(type) };
        if (area.HasValue) titleFacts.Add(areaText);
        if (rent.HasValue) titleFacts.Add(rentText);
        // Only the optional title summary may shorten; every fact remains in the description.
        while (titleFacts.Count > 1 && string.Join(", ", titleFacts).Length > 200) titleFacts.RemoveAt(1);
        var titleSummary = string.Join(", ", titleFacts);
        _titles = new(StringComparer.Ordinal)
        {
            ["brief"] = titleSummary,
            ["detail"] = string.Join(", ", titleFacts.Take(1).Concat(titleFacts.Skip(1).Reverse())),
        };
        var addressText = string.IsNullOrWhiteSpace(address) ? "chưa có thông tin" : NormalizeSourceText(address);
        var amenitiesText = amenities.Length == 0 ? "chưa có thông tin" : string.Join(", ", amenities.Select(NormalizeSourceText));
        var introduction = type switch
        {
            "Phòng trống" => "Mình có phòng cần cho thuê.",
            "Tìm bạn ở ghép" or "Tìm người ở ghép" => "Mình đang tìm bạn ở ghép.",
            "Nhượng phòng" or "Pass phòng" => "Mình cần nhượng lại phòng.",
            _ => $"{NormalizeSourceText(type)}.",
        };
        _sections = new(StringComparer.Ordinal)
        {
            ["type"] = Variants(introduction, $"Tin {NormalizeSourceText(type)}."),
            ["address"] = Variants(string.IsNullOrWhiteSpace(address) ? "Địa chỉ: chưa có thông tin." : $"Phòng ở {addressText}.", string.IsNullOrWhiteSpace(address) ? "Bạn có thể bổ sung địa chỉ cụ thể." : $"Địa chỉ phòng là {addressText}."),
            ["rent"] = Variants($"Tiền thuê là {rentText}.", $"Chi phí thuê là {rentText}."),
            ["area"] = Variants(area.HasValue ? $"Diện tích {areaText}." : "Diện tích: chưa có thông tin hợp lệ.", area.HasValue ? $"Phòng rộng {areaText}." : "Bạn có thể bổ sung diện tích phòng."),
            ["amenities"] = Variants(amenities.Length == 0 ? "Tiện ích: chưa có thông tin." : $"Phòng có {amenitiesText}.", amenities.Length == 0 ? "Bạn có thể bổ sung các tiện ích của phòng." : $"Các tiện ích gồm {amenitiesText}."),
            ["confirmation"] = Variants(
                "Bạn nên trao đổi thêm về tình trạng phòng, tiền điện, nước, internet, tiền cọc và hợp đồng trước khi quyết định.",
                "Hãy hỏi thêm về tình trạng phòng, các khoản điện, nước, internet, tiền cọc và hợp đồng khi liên hệ."),
        };
        // Retain every supplied fact, including the largest valid address and amenity collection.
        if (_titles.Values.Any(value => value.Length > 200) || _sections.Values.Sum(variants => variants.Values.Max(value => value.Length)) + (_sections.Count - 1) * 2 > 4000)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["facts"] = ["Thông tin quá dài để tạo bản nháp đầy đủ. Vui lòng rút gọn nội dung đã nhập."] });
        Prompt = "Chọn cách trình bày bản nháp Homeji bằng catalog IDs. Không soạn prose, không phân tích ảnh, không xác minh dữ liệu hoặc thực thi hành động. "
            + "Chỉ trả JSON đúng schema {\"titleVariantId\":\"brief|detail\",\"sections\":[{\"factId\":\"...\",\"variantId\":\"direct|detail\"}]}. "
            + "Chọn một titleVariantId; sections phải chứa đủ mỗi factId đúng một lần, bạn chọn thứ tự và variant. confirmation phải ở cuối. Không thêm trường, văn bản, Markdown hoặc IDs khác. "
            + "Ưu tiên brief cho tiêu đề. Dùng câu tiếng Việt tự nhiên, ngắn gọn như một tin tìm phòng thông thường. "
            + "Ưu tiên detail cho vị trí, diện tích và tiện ích nếu đã có thông tin; direct cho tiền thuê. Không thêm emoji, dấu sao, gạch đầu dòng hoặc từ ngữ quảng cáo. "
            + "Không chuyển đổi giá theo người/phòng. Các mục chưa biết vẫn phải có để hỏi lại. Catalog: "
            + JsonSerializer.Serialize(new { factIds = _sections.Keys, titleVariantIds = _titles.Keys,
                priceBasis, rentKnown = rent.HasValue, areaKnown = area.HasValue, addressKnown = !string.IsNullOrWhiteSpace(address), amenitiesKnown = amenities.Length > 0 });
    }

    public string Prompt { get; }

    public (string Title, string Description) Render(string reply)
    {
        try
        {
            if (reply.Length > MaxProviderOutputLength) throw new JsonException();
            var json = reply.Trim().ReplaceLineEndings("\n");
            if (json.StartsWith("```json\n", StringComparison.Ordinal) && json.EndsWith("\n```", StringComparison.Ordinal)) json = json[8..^4];
            else if (json.StartsWith("```\n", StringComparison.Ordinal) && json.EndsWith("\n```", StringComparison.Ordinal)) json = json[4..^4];
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            RequireProperties(root, "titleVariantId", "sections");
            var titleId = root.GetProperty("titleVariantId").GetString();
            if (titleId is null || !_titles.TryGetValue(titleId, out var title)) throw new JsonException();
            var sections = root.GetProperty("sections");
            if (sections.ValueKind != JsonValueKind.Array || sections.GetArrayLength() != _sections.Count) throw new JsonException();
            var selected = new HashSet<string>(StringComparer.Ordinal);
            var paragraphs = new List<string>();
            string? lastFactId = null;
            foreach (var section in sections.EnumerateArray())
            {
                RequireProperties(section, "factId", "variantId");
                var factId = section.GetProperty("factId").GetString();
                var variantId = section.GetProperty("variantId").GetString();
                if (factId is null || variantId is null || !selected.Add(factId)
                    || !_sections.TryGetValue(factId, out var variants) || !variants.TryGetValue(variantId, out var text)) throw new JsonException();
                paragraphs.Add(text);
                lastFactId = factId;
            }
            if (lastFactId != "confirmation") throw new JsonException();
            return (title, string.Join(" ", paragraphs.Take(paragraphs.Count - 1)) + "\n\n" + paragraphs[^1]);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new ExternalDependencyException("Gemini chưa trả được bản nháp hợp lệ. Vui lòng thử lại.");
        }
    }

    private static void RequireProperties(JsonElement value, string first, string second)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new JsonException();
        var properties = value.EnumerateObject().Select(property => property.Name).ToArray();
        if (properties.Length != 2 || !properties.Contains(first, StringComparer.Ordinal) || !properties.Contains(second, StringComparer.Ordinal)) throw new JsonException();
    }

    private static Dictionary<string, string> Variants(string direct, string detail) => new(StringComparer.Ordinal) { ["direct"] = direct, ["detail"] = detail };

    internal static string NormalizeSourceText(string value)
    {
        var normalized = new string(value.Select(character => char.IsControl(character) || char.IsWhiteSpace(character)
            || CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.Format ? ' ' : character).ToArray());
        return string.Join(' ', normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

}
