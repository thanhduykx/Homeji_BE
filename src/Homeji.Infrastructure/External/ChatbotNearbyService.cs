using Homeji.Application.Common;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.Services.AI;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.External;

public sealed class ChatbotNearbyService(ApplicationDbContext db, TimeProvider clock) : IChatbotNearbyService
{
    public async Task<ChatbotNearbyDto> SearchAsync(string message, ChatbotLocationDto? location, CancellationToken cancellationToken)
    {
        if (location is null) return new("locationRequired", "Chọn Dùng vị trí của tôi hoặc một địa chỉ bên dưới, rồi gửi lại yêu cầu để mình tìm phòng quanh đúng vị trí đó.", []);
        location.Validate(clock.GetUtcNow());
        if (!HomejiServiceArea.Contains(location.Latitude, location.Longitude)) return new("outOfArea", "Vị trí này nằm ngoài khu vực dữ liệu Thủ Đức / Quận 9 hiện có. Chọn địa chỉ trong khu vực để tìm phòng.", []);
        var criteria = RentalSearchIntent.Apply(message);
        if (criteria.Unknown.Count > 0) return new("clarificationRequired", "Mình cần bạn làm rõ ngân sách, diện tích hoặc điều kiện chưa xác nhận trước khi tìm quanh vị trí đã chọn.", []);
        var lat = location.Latitude; var lng = location.Longitude;
        const decimal radius = 3;
        var dy = radius / 111.32m; var dx = dy / (decimal)Math.Cos((double)lat * Math.PI / 180);
        var native = db.RentalPosts.AsNoTracking().Where(row => row.Status == RentalPostStatus.Active && !row.IsSynthetic
            && row.Latitude >= lat - dy && row.Latitude <= lat + dy && row.Longitude >= lng - dx && row.Longitude <= lng + dx
            && (!criteria.PriceMin.HasValue || row.Price >= criteria.PriceMin) && (!criteria.PriceMax.HasValue || row.Price <= criteria.PriceMax)
            && (!criteria.AreaMin.HasValue || row.Area >= criteria.AreaMin) && (!criteria.AreaMax.HasValue || row.Area <= criteria.AreaMax)
            && (!criteria.Occupants.HasValue || row.AvailableSlots >= criteria.Occupants)
            && (!criteria.ExcludeRoommateShare || row.Type == RentalPostType.VacantRoom));
        foreach (var code in criteria.RequiredAmenities) native = native.Where(row => row.Amenities.Any(amenity => amenity.Code == code));
        foreach (var code in criteria.ExcludedAmenities) native = native.Where(row => !row.Amenities.Any(amenity => amenity.Code == code));
        var nativeRows = await native.OrderBy(row => row.Id).Take(1000).Select(row => new { row.Id, row.Title, row.Address, row.Price, row.Area, row.Latitude, row.Longitude }).ToArrayAsync(cancellationToken);
        var items = nativeRows.Select(row => new ChatbotNearbyItemDto(row.Id, false, row.Title, row.Address, row.Price, row.Area, Distance(lat, lng, row.Latitude, row.Longitude))).ToList();
        var now = clock.GetUtcNow();
        var includeImported = criteria.RequiredAmenities.Count == 0 && criteria.ExcludedAmenities.Count == 0 && !criteria.Occupants.HasValue && !criteria.ExcludeRoommateShare;
        if (includeImported)
        {
            var rows = await db.RentalSourceListings.AsNoTracking().Where(row => row.Latitude != null && row.Longitude != null
                && row.Latitude >= lat - dy && row.Latitude <= lat + dy && row.Longitude >= lng - dx && row.Longitude <= lng + dx
                && (row.SourceExpiresAt == null || row.SourceExpiresAt > now)
                && (!criteria.PriceMin.HasValue || row.Price >= criteria.PriceMin) && (!criteria.PriceMax.HasValue || row.Price <= criteria.PriceMax)
                && (!criteria.AreaMin.HasValue || row.Area >= criteria.AreaMin) && (!criteria.AreaMax.HasValue || row.Area <= criteria.AreaMax))
                .OrderBy(row => row.Id).Take(1000).Select(row => new { row.Id, row.Title, row.Address, row.Price, row.Area, row.Latitude, row.Longitude }).ToArrayAsync(cancellationToken);
            items.AddRange(rows.Select(row => new ChatbotNearbyItemDto(row.Id, true, row.Title, row.Address, row.Price, row.Area, Distance(lat, lng, row.Latitude!.Value, row.Longitude!.Value))));
        }
        var matches = items.Where(row => row.DistanceKm <= (double)radius).OrderBy(row => row.DistanceKm).ThenBy(row => row.Id).Take(8).ToArray();
        return new(matches.Length == 0 ? "empty" : "results", matches.Length == 0
            ? "Chưa có phòng khớp điều kiện trong bán kính 3 km từ vị trí bạn chia sẻ. Bạn có thể đổi địa chỉ hoặc điều kiện tìm kiếm."
            : "Đây là các phòng khớp bộ lọc trong bán kính 3 km từ vị trí bạn chia sẻ, xếp gần trước. Khoảng cách là đường thẳng; liên hệ để xác nhận còn phòng.", matches, radius);
    }

    public static double Distance(decimal lat, decimal lng, decimal otherLat, decimal otherLng)
    {
        var a = (double)lat * Math.PI / 180; var b = (double)otherLat * Math.PI / 180;
        var dLat = b - a; var dLng = (double)(otherLng - lng) * Math.PI / 180;
        var h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(a) * Math.Cos(b) * Math.Pow(Math.Sin(dLng / 2), 2);
        return Math.Round(6371 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(h, 0, 1))), 3);
    }
}
