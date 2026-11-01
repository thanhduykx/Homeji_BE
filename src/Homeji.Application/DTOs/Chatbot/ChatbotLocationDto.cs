using Homeji.Application.Common.Exceptions;

namespace Homeji.Application.DTOs.Chatbot;

public sealed record ChatbotLocationDto(decimal Latitude, decimal Longitude, string? Address,
    DateTimeOffset CapturedAt, decimal? AccuracyMeters = null)
{
    public void Validate(DateTimeOffset now)
    {
        if (Latitude is < -90 or > 90 || Longitude is < -180 or > 180 || Latitude == 0 && Longitude == 0
            || Address?.Length > 500 || Address?.Any(char.IsControl) == true || AccuracyMeters is < 0 or > 100000
            || CapturedAt > now.AddMinutes(1) || CapturedAt < now.AddMinutes(-15))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["location"] = ["Vị trí không hợp lệ hoặc đã cũ. Chọn lại địa chỉ hay chia sẻ lại vị trí hiện tại."] });
    }
}
