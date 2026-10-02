using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.Chatbot;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotNavigationCatalogTests
{
    [Fact]
    public void FindActions_WhenUserAsksHowToBuyFood_ReturnsMarketplaceButton()
    {
        var actions = ChatbotNavigationCatalog.FindActions(
            "Mình muốn mua đồ ăn thì làm như nào?",
            UserRole.Renter);

        var action = Assert.Single(actions);
        Assert.Equal("marketplace-food", action.Id);
        Assert.Equal(ChatbotNavigationActionKind.OpenSection, action.Kind);
        Assert.Equal("marketplace:food", action.Target);
        Assert.Contains("không tự tạo đơn", action.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindActions_WhenUserAsksForCart_ReturnsCartReviewButton()
    {
        var actions = ChatbotNavigationCatalog.FindActions(
            "Mở giỏ hàng để mình kiểm tra và xác nhận đặt món",
            UserRole.Renter);

        var action = Assert.Single(actions);
        Assert.Equal("marketplace-cart", action.Id);
        Assert.Equal(ChatbotNavigationActionKind.OpenSection, action.Kind);
        Assert.Equal("marketplace:cart", action.Target);
        Assert.Contains("xác nhận", action.Label, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindActions_WhenRenterAsksToPassRoom_ReturnsRoleSpecificCreateButton()
    {
        var actions = ChatbotNavigationCatalog.FindActions(
            "Hướng dẫn mình pass phòng và chuyển hợp đồng",
            UserRole.Renter);

        var action = Assert.Single(actions);
        Assert.Equal("create-transfer", action.Id);
        Assert.Equal("/posts/new?type=pass", action.Target);
    }

    [Fact]
    public void FindActions_WhenLandlordAsksToPassRoom_DoesNotExposeRenterCreateRoute()
    {
        var actions = ChatbotNavigationCatalog.FindActions(
            "Hướng dẫn mình pass phòng và chuyển hợp đồng",
            UserRole.Landlord);

        Assert.DoesNotContain(actions, action => action.Id == "create-transfer");
    }

    [Fact]
    public void FindActions_WhenQuestionHasSeveralFeatures_ReturnsDistinctBoundedButtons()
    {
        var actions = ChatbotNavigationCatalog.FindActions(
            "Mở thông báo, lịch xem phòng, hồ sơ và tin nhắn",
            UserRole.Renter);

        Assert.Equal(ChatbotNavigationCatalog.MaxActionsPerReply, actions.Count);
        Assert.Equal(actions.Count, actions.Select(action => action.Id).Distinct().Count());
    }
}
