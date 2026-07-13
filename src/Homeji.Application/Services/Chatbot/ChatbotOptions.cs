namespace Homeji.Application.Services.Chatbot;

public sealed class ChatbotOptions
{
    public const string SectionName = "Chatbot";

    public bool Enabled { get; set; } = true;

    public string Title { get; set; } = "Homeji Assistant";

    public string Greeting { get; set; } = "Xin chào, mình là Homeji Assistant. Bạn cần tìm phòng, hỏi về bài đăng hay thanh toán Premium?";

    public int MaxHistoryMessages { get; set; } = 12;

    public int SearchResultLimit { get; set; } = 5;

    public string[] SuggestedPrompts { get; set; } =
    [
        "Tìm phòng gần Đại học FPT dưới 2 triệu",
        "Premium có lợi ích gì?",
        "Cách thanh toán bằng PayOS như thế nào?",
    ];
}
