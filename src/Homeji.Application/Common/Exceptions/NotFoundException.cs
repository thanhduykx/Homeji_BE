namespace Homeji.Application.Common.Exceptions;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string resourceName, object key)
        : base($"Không tìm thấy {LocalizeResource(resourceName)} với mã '{key}'.")
    {
    }

    private static string LocalizeResource(string resourceName) => resourceName switch
    {
        "RentalPost" => "tin đăng phòng trọ",
        "MarketplacePost" => "tin chợ đồ",
        "MarketplaceOrder" => "đơn chợ đồ",
        "RentalWantedPost" => "tin tìm phòng",
        "PostConversation" => "cuộc trò chuyện",
        "RoommateConversation" => "cuộc trò chuyện ở ghép",
        "RoommateInvitation" => "lời mời ở ghép",
        "RentalReview" => "đánh giá",
        "ViewingAppointment" => "lịch xem phòng",
        "PaymentTransaction" => "giao dịch thanh toán",
        "UserProfile" => "hồ sơ người dùng",
        "Notification" => "thông báo",
        "Report" => "báo cáo",
        "ChatConversation" => "cuộc trò chuyện chatbot",
        "LandlordVerificationRequest" => "yêu cầu xác minh chủ trọ",
        "RentalPostMedia" => "ảnh tin đăng",
        _ => resourceName,
    };
}
