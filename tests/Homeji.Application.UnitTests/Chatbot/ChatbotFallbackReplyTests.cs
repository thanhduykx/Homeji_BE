using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.Chatbot;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotFallbackReplyTests
{
    [Fact]
    public void Create_WhenModelIsUnavailable_ExplainsFailureInsteadOfPretendingToAnswer()
    {
        var reply = ChatbotFallbackReply.Create([]);

        Assert.Contains("AI đang tạm thời không khả dụng", reply, StringComparison.Ordinal);
        Assert.Contains("thử lại", reply, StringComparison.Ordinal);
        Assert.DoesNotContain("chế độ hỗ trợ cơ bản", reply, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_WhenNavigationIsAvailable_KeepsManualNavigationAndConfirmation()
    {
        var action = new ChatbotNavigationActionDto("marketplace-cart", "Giỏ hàng", "Mở giỏ hàng",
            ChatbotNavigationActionKind.OpenSection, "marketplace");

        var reply = ChatbotFallbackReply.Create([action]);

        Assert.Contains("AI đang tạm thời không khả dụng", reply, StringComparison.Ordinal);
        Assert.Contains("giỏ hàng", reply, StringComparison.Ordinal);
        Assert.Contains("xác nhận", reply, StringComparison.Ordinal);
    }
}
