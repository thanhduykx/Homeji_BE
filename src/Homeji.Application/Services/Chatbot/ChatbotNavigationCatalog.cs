using System.Globalization;
using System.Text;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Chatbot;

public static class ChatbotNavigationCatalog
{
    public const int MaxActionsPerReply = 3;

    public const string FeaturePrompt = """
        Các tính năng thật đang có trong Homeji:
        - Tìm phòng trên bản đồ, lọc khu vực, ngân sách, tiện ích và xem chi tiết tin.
        - Lưu tin phòng; đăng tin tìm phòng; quản lý tin đã đăng.
        - Tìm bạn ở ghép, gửi/nhận lời mời và nhắn tin sau khi kết nối.
        - Đặt và quản lý lịch xem phòng.
        - Pass phòng: người thuê đăng tin chuyển hợp đồng/cho thuê lại và gửi kiểm duyệt.
        - Chợ đồ: mua đồ ăn, mua/bán đồ dùng, giỏ hàng, đơn mua, đơn bán và ví Chợ đồ.
        - Gói đăng ký/Premium và thanh toán MoMo hoặc PayOS.
        - Hồ sơ, thông báo và nhật ký hoạt động.
        - Chủ phòng có thể đăng phòng trống hoặc tin tìm người ở ghép; quản trị viên có khu vực kiểm duyệt.

        Khi người dùng hỏi cách dùng một tính năng:
        - Trả lời bằng các bước đúng với danh sách trên; không bịa màn hình hoặc chức năng chưa có.
        - Nếu có nút điều hướng phù hợp, kết thúc bằng một câu ngắn như “Bạn có thể bấm nút bên dưới để mở ngay”.
        - Không tự viết URL, đường dẫn Markdown hay tên route trong câu trả lời; hệ thống sẽ gắn button an toàn riêng.
        """;

    private static readonly CatalogEntry[] CommonEntries =
    [
        Entry("marketplace-food", "Mở Chợ đồ ăn", "Chọn món, thêm vào giỏ và đặt đơn.", ChatbotNavigationActionKind.OpenSection, "marketplace",
            "mua đồ ăn", "đặt đồ ăn", "đặt món", "mua thức ăn", "đồ ăn"),
        Entry("marketplace-sell", "Mở Chợ đồ để đăng bán", "Đăng món ăn hoặc đồ dùng cho sinh viên.", ChatbotNavigationActionKind.OpenSection, "marketplace",
            "đăng bán", "bán đồ", "bán thức ăn", "bán món"),
        Entry("marketplace", "Mở Chợ đồ", "Mua bán đồ dùng, đồ ăn và quản lý đơn hàng.", ChatbotNavigationActionKind.OpenSection, "marketplace",
            "chợ đồ", "mua đồ", "giỏ hàng", "đơn hàng", "đơn mua", "đơn bán", "ví chợ"),
        Entry("listings", "Tìm phòng trên bản đồ", "Mở danh sách và bộ lọc phòng.", ChatbotNavigationActionKind.OpenSection, "listings",
            "tìm phòng", "tìm trọ", "phòng gần", "lọc phòng", "bản đồ phòng"),
        Entry("saved", "Mở tin đã lưu", "Xem lại những phòng bạn quan tâm.", ChatbotNavigationActionKind.OpenSection, "saved",
            "tin đã lưu", "phòng đã lưu", "yêu thích"),
        Entry("wanted", "Mở tin tìm phòng", "Đăng hoặc quản lý nhu cầu tìm phòng.", ChatbotNavigationActionKind.OpenSection, "wanted",
            "tin tìm phòng", "đăng nhu cầu", "cần tìm phòng"),
        Entry("roommate", "Mở khu vực Ở ghép", "Xem và xử lý lời mời ở ghép.", ChatbotNavigationActionKind.OpenSection, "invitations",
            "ở ghép", "bạn cùng phòng", "roommate", "lời mời ở ghép"),
        Entry("messages", "Mở Tin nhắn", "Xem hội thoại với người cho thuê hoặc người ở ghép.", ChatbotNavigationActionKind.OpenSection, "messages",
            "tin nhắn", "nhắn tin", "hội thoại", "chat"),
        Entry("appointments", "Mở lịch xem phòng", "Xem và quản lý các lịch hẹn.", ChatbotNavigationActionKind.OpenSection, "appointments",
            "lịch xem phòng", "đặt lịch", "lịch hẹn", "xem phòng"),
        Entry("payments", "Mở Gói đăng ký", "Xem Premium và trạng thái thanh toán.", ChatbotNavigationActionKind.OpenSection, "payments",
            "premium", "gói đăng ký", "momo", "payos", "thanh toán", "nâng cấp"),
        Entry("profile", "Mở hồ sơ", "Cập nhật thông tin và xác minh tài khoản.", ChatbotNavigationActionKind.OpenSection, "profile",
            "hồ sơ", "profile", "tài khoản", "xác minh", "đổi thông tin"),
        Entry("notifications", "Mở thông báo", "Xem các cập nhật mới trong Homeji.", ChatbotNavigationActionKind.OpenSection, "notifications",
            "thông báo", "notification"),
        Entry("activities", "Mở nhật ký hoạt động", "Xem lại các thao tác gần đây.", ChatbotNavigationActionKind.OpenSection, "activities",
            "nhật ký", "hoạt động", "lịch sử"),
        Entry("my-posts", "Mở Tin của tôi", "Quản lý các tin bạn đã đăng.", ChatbotNavigationActionKind.OpenSection, "myPosts",
            "tin của tôi", "quản lý tin", "tin đã đăng"),
    ];

    public static IReadOnlyCollection<ChatbotNavigationActionDto> FindActions(string message, UserRole role)
    {
        var normalized = Normalize(message);
        var matches = CommonEntries
            .Where(entry => entry.Terms.Any(normalized.Contains))
            .Select(entry => entry.Action)
            .ToList();

        AddRoleSpecificActions(matches, normalized, role);

        return matches
            .DistinctBy(action => (action.Kind, action.Target))
            .Take(MaxActionsPerReply)
            .ToArray();
    }

    private static void AddRoleSpecificActions(
        List<ChatbotNavigationActionDto> actions,
        string normalized,
        UserRole role)
    {
        if (role == UserRole.Renter
            && ContainsAny(normalized, "pass phong", "sang phong", "chuyen hop dong", "cho thue lai"))
        {
            actions.Add(new ChatbotNavigationActionDto(
                "create-transfer", "Đăng tin pass phòng", "Tạo tin chuyển hợp đồng hoặc cho thuê lại.",
                ChatbotNavigationActionKind.Navigate, "/posts/new?type=pass"));
        }

        if (role == UserRole.Landlord
            && ContainsAny(normalized, "dang phong", "cho thue phong", "dang tin phong", "tim nguoi o ghep"))
        {
            actions.Add(new ChatbotNavigationActionDto(
                "create-rental", "Đăng tin phòng", "Tạo tin phòng trống hoặc tìm người ở ghép.",
                ChatbotNavigationActionKind.Navigate, "/posts/new"));
        }

        if (role == UserRole.Admin
            && ContainsAny(normalized, "kiem duyet", "quan tri", "bao cao vi pham"))
        {
            actions.Add(new ChatbotNavigationActionDto(
                "admin", "Mở trang quản trị", "Kiểm duyệt tin và xử lý báo cáo.",
                ChatbotNavigationActionKind.Navigate, "/admin"));
        }
    }

    private static bool ContainsAny(string text, params string[] terms) => terms.Any(text.Contains);

    private static CatalogEntry Entry(
        string id,
        string label,
        string description,
        ChatbotNavigationActionKind kind,
        string target,
        params string[] terms)
    {
        return new CatalogEntry(
            new ChatbotNavigationActionDto(id, label, description, kind, target),
            terms.Select(Normalize).ToArray());
    }

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record CatalogEntry(
        ChatbotNavigationActionDto Action,
        IReadOnlyCollection<string> Terms);
}
