namespace Homeji.Application.Services.Chatbot;

public sealed class ChatbotOptions
{
    public const string SectionName = "Chatbot";

    public bool Enabled { get; set; } = true;

    public string Title { get; set; } = "Homeji";

    public string Greeting { get; set; } = "Xin chào, mình là trợ lý Homeji. Mình có thể tìm phòng, mở đúng tính năng và hỗ trợ bạn chọn đồ ăn an toàn.";

    public int MaxHistoryMessages { get; set; } = 12;

    public int SearchResultLimit { get; set; } = 5;

    public string[] SuggestedPrompts { get; set; } =
    [
        "Tìm phòng gần Đại học FPT dưới 2 triệu",
        "Mua đồ ăn trên Homeji như thế nào?",
        "Premium có lợi ích gì?",
        "Cách thanh toán bằng PayOS như thế nào?",
    ];
}
