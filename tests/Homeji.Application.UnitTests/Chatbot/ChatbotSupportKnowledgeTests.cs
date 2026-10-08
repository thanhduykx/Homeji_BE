using Homeji.Application.Services.Chatbot;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotSupportKnowledgeTests
{
    [Theory]
    [InlineData("HI!")]
    [InlineData("Xin chào")]
    [InlineData("  hello  ")]
    public void Greeting_UsesReviewedReply(string text) => Assert.StartsWith("Xin chào!", ChatbotSupportKnowledge.FindReply(text));

    [Theory]
    [InlineData("hi, tìm phòng dưới 3 triệu")]
    [InlineData("history of Vietnam")]
    [InlineData("Tìm phòng có bếp")]
    [InlineData("Thời tiết hôm nay?")]
    public void UnknownOrRentalQuestion_IsNotReplacedByGreeting(string text) => Assert.Null(ChatbotSupportKnowledge.FindReply(text));

    [Fact]
    public void PayOsInstructions_ExplainConfirmedCheckoutWithoutClaimingSuccess()
    {
        var reply = ChatbotSupportKnowledge.FindReply("Cách thanh toán bằng PayOS như thế nào?")!;
        Assert.Contains("Thanh toán qua PayOS", reply);
        Assert.Contains("Kiểm tra lại", reply);
        Assert.Contains("hệ thống xác nhận", reply);
        Assert.Contains("15 phút", reply);
    }

    [Theory]
    [InlineData("Đã thanh toán PayOS nhưng chưa kích hoạt")]
    [InlineData("PayOS bị trừ tiền")]
    [InlineData("Đã trả PayOS rồi")]
    public void PaymentClaim_NeverInfersLiveState(string text)
    {
        var reply = ChatbotSupportKnowledge.FindReply(text)!;
        Assert.Contains("chưa có dữ liệu giao dịch", reply);
        Assert.Contains("Lịch sử giao dịch", reply);
        Assert.DoesNotContain("đã kích hoạt", reply);
    }

    [Fact]
    public void AmbiguousPayOsQuestion_AsksOneClarification() => Assert.Contains("hay kiểm tra", ChatbotSupportKnowledge.FindReply("PayOS lỗi rồi"));
}
