using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.AI;

public static class RentalCostCalculator
{
    public static RentalCostEstimateDto Calculate(RentalPost post, RentalCostScenarioDto scenario)
    {
        string[] validUnits = ["unknown", "month", "person", "kwh", "m3"];
        if (scenario.Occupants is < 1 or > 50 || scenario.ElectricityKwh is < 0 or > 10000 || scenario.WaterM3 is < 0 or > 1000
            || !validUnits.Contains(scenario.ElectricityUnit, StringComparer.Ordinal)
            || !validUnits.Contains(scenario.WaterUnit, StringComparer.Ordinal)
            || scenario.InternetUnit is not ("unknown" or "month" or "person")
            || scenario.ElectricityUnit == "m3" || scenario.WaterUnit == "kwh"
            || scenario.OtherMonthlyFees is < 0 or > 100_000_000 || scenario.OtherInitialFees is < 0 or > 100_000_000)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["scenario"] = ["Kịch bản sử dụng hoặc đơn vị phí không hợp lệ."] });
        var unknown = new List<string>();
        decimal Fee(string label, decimal rate, string unit, decimal usage, bool confirmedFree)
        {
            if (rate == 0 && confirmedFree) return 0;
            if (rate <= 0 || unit == "unknown") { unknown.Add(label + ": chưa xác nhận đơn giá/đơn vị; 0 không có nghĩa miễn phí."); return 0; }
            return rate * (unit == "person" ? scenario.Occupants : unit is "kwh" or "m3" ? usage : 1);
        }
        var subtotal = post.Price + Fee("Điện", post.ElectricityPrice, scenario.ElectricityUnit, scenario.ElectricityKwh, scenario.ElectricityFreeConfirmed)
            + Fee("Nước", post.WaterPrice, scenario.WaterUnit, scenario.WaterM3, scenario.WaterFreeConfirmed)
            + Fee("Internet", post.InternetPrice, scenario.InternetUnit, 1, scenario.InternetFreeConfirmed)
            + (scenario.OtherMonthlyFees ?? 0);
        // Existing posts have no authoritative fixed-fee completeness marker.
        if (!scenario.OtherMonthlyFees.HasValue) unknown.Add("Phí cố định khác (giữ xe, dịch vụ, rác): cần hỏi chủ phòng.");
        var questions = new List<string>
        {
            "Điện tính theo kWh, tháng hay đầu người? Đơn giá có bao gồm thuế/phụ phí?",
            "Nước tính theo m³, tháng hay đầu người? Internet và phí giữ xe/dịch vụ/rác bao nhiêu?",
            "Hợp đồng bao lâu, điều kiện hoàn cọc và khoản thu ban đầu là gì?",
            "Giờ giấc ra vào và quy định nuôi thú cưng như thế nào?",
        };
        if (post.Deposit <= 0) questions.Add("Tiền cọc chưa được xác nhận; có thực sự không thu cọc?");
        return new RentalCostEstimateDto(post.Id, subtotal, unknown.Count == 0 ? subtotal : null,
            (post.Deposit > 0 || scenario.DepositFreeConfirmed) && scenario.OtherInitialFees.HasValue
                ? post.Price + post.Deposit + (post.Type == RentalPostType.RoomTransfer ? post.PassFee : 0) + scenario.OtherInitialFees.Value : null,
            unknown, questions, "ownerListing + userScenario", post.UpdatedAt);
    }
}
