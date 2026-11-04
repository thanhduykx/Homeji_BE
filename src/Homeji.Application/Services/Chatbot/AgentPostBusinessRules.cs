using Homeji.Application.Common;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
namespace Homeji.Application.Services.Chatbot;

public static class AgentPostBusinessRules
{
    public static void Validate(AgentPostDetails details, DateTimeOffset now)
    {
        var errors = Collect(details, now);
        if (errors.Count > 0) throw new RequestValidationException(errors);
    }

    /// <summary>What the user still has to provide before this draft can be confirmed.</summary>
    public static IReadOnlyList<string> Missing(AgentPostDetails details, DateTimeOffset now, int imageCount, int minimumImages)
    {
        var missing = Collect(details, now).Values.SelectMany(messages => messages).ToList();
        if (details.Kind != "wanted" && imageCount < minimumImages) missing.Add($"Thêm ảnh thật: cần ít nhất {minimumImages} ảnh, hiện có {imageCount}.");
        return missing;
    }

    private static Dictionary<string, string[]> Collect(AgentPostDetails details, DateTimeOffset now)
    {
        var errors = new Dictionary<string, string[]>();
        void Require(bool valid, string field, string message) { if (!valid) errors[field] = [message]; }
        Require(details.Kind is "secondHand" or "food" or "rental" or "roomTransfer" or "roommate" or "wanted", "kind", "Loại tin không hợp lệ.");
        Require(!string.IsNullOrWhiteSpace(details.Title) && details.Title.Length <= 200, "title", "Nhập tiêu đề tối đa 200 ký tự.");
        Require(!string.IsNullOrWhiteSpace(details.Description) && details.Description.Length <= 3000, "description", "Nhập mô tả tối đa 3000 ký tự.");
        Require(details.Price > 0 && details.Price <= 1_000_000_000 && decimal.Truncate(details.Price) == details.Price, "price", "Nhập giá hoặc ngân sách bằng số nguyên dương.");
        Require(!string.IsNullOrWhiteSpace(details.Address) && details.Address.Length <= 500, "address", "Bổ sung địa chỉ hoặc khu vực tìm trọ.");
        Require(details.Amenities is not null && details.Amenities.Length <= 30, "amenities", "Danh sách tiện ích không hợp lệ.");
        if (details.Kind != "wanted") Require(HomejiServiceArea.Contains(details.Latitude, details.Longitude), "location", "Chọn vị trí thật trong Thủ Đức và Quận 9 cũ.");
        if (details.Kind is "rental" or "roomTransfer" or "roommate")
        {
            Require(details.Deposit is >= 0 and <= 1_000_000_000, "deposit", "Nhập tiền cọc; nhập 0 nếu xác nhận không có cọc.");
            Require(details.Area is > 0 and <= 100000, "area", "Bổ sung diện tích phòng.");
            Require(details.MaxOccupants is > 0 and <= 100, "maxOccupants", "Bổ sung số người tối đa.");
            Require(details.AvailableSlots is > 0 && details.AvailableSlots <= details.MaxOccupants, "availableSlots", "Bổ sung số chỗ trống không vượt số người tối đa.");
            Require(details.MinimumLeaseMonths is null or > 0 and <= 120, "minimumLeaseMonths", "Thời hạn thuê từ 1 đến 120 tháng.");
        }
        if (details.Kind == "roomTransfer")
        {
            Require(details.TransferKind is 1 or 2, "transferKind", "Chọn chuyển hợp đồng hoặc cho thuê lại.");
            Require(details.AvailableFrom.HasValue && details.OriginalLeaseEndsOn > details.AvailableFrom, "originalLeaseEndsOn", "Bổ sung ngày nhận phòng và ngày hợp đồng kết thúc.");
            Require(details.OwnerConsentConfirmed && !string.IsNullOrWhiteSpace(details.OwnerConsentContact), "ownerConsentConfirmed", "Bạn phải tự xác nhận chủ nhà đồng ý và nhập liên hệ chủ nhà.");
            Require(!string.IsNullOrWhiteSpace(details.TransferReason), "transferReason", "Bổ sung lý do pass phòng.");
            Require(details.PassFee is >= 0 and <= 1_000_000_000, "passFee", "Nhập phí pass; nhập 0 nếu không có phí.");
        }
        if (details.Kind == "wanted")
        {
            Require(details.MaxOccupants is > 0 and <= 100, "maxOccupants", "Nhập số người muốn thuê.");
            Require(details.AvailableFrom >= DateOnly.FromDateTime(now.UtcDateTime), "availableFrom", "Chọn ngày dự kiến chuyển vào từ hôm nay.");
        }
        if (details.Kind == "food")
        {
            Require(details.AvailableQuantity is > 0 and <= 100, "availableQuantity", "Nhập số lượng món đang bán.");
            Require(!string.IsNullOrWhiteSpace(details.Unit) && details.Unit.Length <= 30, "unit", "Nhập đơn vị bán: phần, ly, hộp…");
            Require(details.PreparationMinutes is >= 0 and <= 240, "preparationMinutes", "Nhập thời gian chuẩn bị từ 0 đến 240 phút.");
        }
        return errors;
    }
}
