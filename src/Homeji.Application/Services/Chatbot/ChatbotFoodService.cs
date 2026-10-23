using System.Globalization;
using System.Text.RegularExpressions;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.DTOs.Marketplace;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.IServices.Marketplace;
using Homeji.Application.Services.AI;
using Homeji.Application.Services.Common;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Chatbot;

public sealed partial class ChatbotFoodService(IMarketplacePostService marketplace) : IChatbotFoodService
{
    public async Task<ChatbotFoodResponseDto> SearchAsync(string message, ChatbotFoodContextDto? origin,
        CancellationToken cancellationToken = default)
    {
        if (origin is not null && (origin.Latitude is < -90 or > 90 || origin.Longitude is < -180 or > 180
            || origin.RadiusKm is <= 0 or > 10))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["foodContext"] = ["Chọn mốc hợp lệ và bán kính từ trên 0 đến 10 km."] });

        var text = RentalSearchIntent.Normalize(message);
        // Dietary/allergy claims are unsupported by the public catalogue. Never infer them from a title.
        if (DietaryTerms().IsMatch(text))
            return new("clarificationRequired", "Tin món ăn chưa có thành phần và thông tin dị ứng được xác nhận. Hãy kiểm tra thực đơn và hỏi người bán trước khi chọn món theo chế độ ăn.", []);

        var keyword = DishKeywords.FirstOrDefault(pair => Regex.IsMatch(text, $@"\b{pair.Key}\b",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))).Value;
        decimal? maxPrice = null;
        var money = Budget().Match(text);
        if (money.Success && decimal.TryParse(money.Groups["amount"].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
        {
            var multiplier = money.Groups["unit"].Value is "k" or "nghin" or "ngan" ? 1_000 : 1;
            if (amount > 0 && amount <= 10_000_000m / multiplier) maxPrice = amount * multiplier;
        }
        if (PriceTerms().IsMatch(text) && maxPrice is null)
            return new("clarificationRequired", "Bạn muốn giá món tối đa bao nhiêu? Ví dụ: ‘Gợi ý đồ ăn dưới 40k’. Giá món chưa gồm phí giao và khoản khác.", [], keyword);
        if (FoodSearchIntent.NeedsLocation(message) && origin is null)
            return new("locationRequired", "Bạn muốn ăn quanh đâu? Chọn địa chỉ hoặc mốc bên dưới để mình tìm món đang bán trong bán kính bạn chọn.", [], keyword, maxPrice);

        try
        {
            var posts = await marketplace.SearchAsync(new MarketplaceSearchDto(keyword, null,
                MarketplaceListingType.Food, null, maxPrice, origin?.Latitude, origin?.Longitude,
                origin?.RadiusKm, null, 1, 5), cancellationToken);
            var items = posts.Where(post => post.ListingType == MarketplaceListingType.Food
                    && post.Status == MarketplacePostStatus.Active && !post.IsSynthetic && post.AvailableQuantity > 0
                    && (!maxPrice.HasValue || post.Price <= maxPrice)
                    && (origin is null || post.DistanceKm is >= 0 && post.DistanceKm <= origin.RadiusKm))
                .Take(5).Select(post => new ChatbotFoodItemDto(post.Id, post.SellerId, post.SellerDisplayName,
                    post.Title, post.Price, post.Unit, post.AvailableQuantity, post.PreparationMinutes,
                    origin is null ? null : post.DistanceKm, post.UpdatedAt)).ToArray();
            return new(items.Length == 0 ? "empty" : "results", items.Length == 0
                ? "Chưa có món đang bán khớp bộ lọc này trên Homeji. Bạn có thể đổi mốc, tăng bán kính hoặc xem toàn bộ Chợ đồ."
                : origin is null ? "Đây là một số món đang bán trên Homeji, theo tin cập nhật gần nhất. Chọn mốc nếu bạn muốn tìm gần mình."
                : "Đây là các món đang bán trong bán kính đã chọn, xếp theo khoảng cách đường thẳng từ mốc của bạn.",
                items, keyword, maxPrice, origin);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is ExternalDependencyException or ExternalServiceUnavailableException or HttpRequestException or TaskCanceledException)
        {
            return new("unavailable", "Chưa tải được món đang bán. Hãy thử lại hoặc mở Chợ đồ để xem thực đơn.", [], keyword, maxPrice, origin);
        }
    }

    private static readonly KeyValuePair<string, string>[] DishKeywords =
    [new("com ga", "cơm gà"), new("com tam", "cơm tấm"), new("bun bo", "bún bò"),
     new("banh mi", "bánh mì"), new("mi xao", "mì xào"), new("tra sua", "trà sữa"),
     new("com", "cơm"), new("bun", "bún"), new("pho", "phở")];

    [GeneratedRegex(@"\b(?:duoi|toi da|khong qua|ngan sach)\s*(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>k|nghin|ngan|d|dong)\b")]
    private static partial Regex Budget();
    [GeneratedRegex(@"\b(duoi|toi da|khong qua|ngan sach|gia|re hon|tu \d|tren \d|\d+\s*(?:k|nghin|ngan|dong))\b")]
    private static partial Regex PriceTerms();
    [GeneratedRegex(@"\b(chay|di ung|khong (?:cay|duong|sua|dau|thit|an|co)|it (?:cay|duong|muoi)|keto|halal|gluten|healthy)\b")]
    private static partial Regex DietaryTerms();
}
