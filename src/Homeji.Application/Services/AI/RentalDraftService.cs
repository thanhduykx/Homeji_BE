using System.Globalization;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Homeji.Application.IServices.Chatbot;

namespace Homeji.Application.Services.AI;

public sealed class RentalDraftService(IChatbotAiClient gemini) : IRentalDraftService
{
    public async Task<RentalDraftResponseDto> GenerateAsync(RentalDraftRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request.PriceBasis is not null and not ("room" or "person" or "unknown"))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["priceBasis"] = ["Đơn vị giá phải là room, person hoặc unknown."] });
        var priceBasis = request.PriceBasis ?? "unknown";
        if (string.IsNullOrWhiteSpace(request.TypeLabel) || request.TypeLabel.Length > 80
            || request.Address?.Length > 500 || request.Rent?.Length > 30 || request.Area?.Length > 30
            || request.Amenities is null || request.Amenities.Length > 20
            || request.Amenities.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 80)
            || request.ImageCount is < 0 or > 30)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["facts"] = ["Thông tin soạn tin không hợp lệ."] });
        var type = GroundedRentalDraftCatalog.NormalizeSourceText(request.TypeLabel);
        var address = GroundedRentalDraftCatalog.NormalizeSourceText(request.Address ?? "");
        var amenities = request.Amenities.Select(GroundedRentalDraftCatalog.NormalizeSourceText).ToArray();
        if (type.Length == 0 || amenities.Any(value => value.Length == 0))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["facts"] = ["Loại tin và tiện ích phải chứa nội dung hiển thị được."] });
        var rent = PositiveNumber(request.Rent, 1_000_000_000);
        var area = PositiveNumber(request.Area, 100_000);
        var missing = new List<string>();
        if (rent is null) missing.Add("Giá thuê hợp lệ");
        if (priceBasis == "unknown") missing.Add("Đơn vị tiền thuê theo phòng hoặc theo người mỗi tháng chưa xác nhận");
        if (area is null) missing.Add("Diện tích do chủ tin xác nhận");
        if (address.Length == 0) missing.Add("Địa chỉ cụ thể");
        if (request.ImageCount < 3) missing.Add("Ít nhất 3 ảnh thật có quyền sử dụng");
        missing.Add("Đơn vị và đơn giá điện, nước, internet; điều kiện cọc và hợp đồng");
        var catalog = new GroundedRentalDraftCatalog(type, address, rent, area, amenities, priceBasis);
        var reply = await gemini.GenerateReplyAsync([], catalog.Prompt, cancellationToken);
        var (title, description) = catalog.Render(reply);
        return new(title, description, missing.ToArray());
    }

    private static decimal? PositiveNumber(string? value, decimal maximum) =>
        decimal.TryParse(value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
        && number > 0 && number <= maximum ? number : null;
}
