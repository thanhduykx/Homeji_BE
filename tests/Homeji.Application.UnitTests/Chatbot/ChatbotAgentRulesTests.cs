using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.Chatbot;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotAgentRulesTests
{
    [Fact]
    public void Offer_has_a_shared_fixed_window_and_never_changes_regular_plan_options()
    {
        var start = DateTimeOffset.UtcNow;
        var options = new PremiumSubscriptionOptions { LaunchOfferStartsAt = start, LaunchOfferEndsAt = start.AddDays(7) };
        Assert.Equal(99000, Assert.Single(options.GetPlansAt(start.AddSeconds(-1))).Price);
        Assert.Equal(49000, Assert.Single(options.GetPlansAt(start)).Price);
        Assert.Equal(49000, Assert.Single(options.GetPlansAt(start.AddDays(7).AddTicks(-1))).Price);
        Assert.Equal(99000, Assert.Single(options.GetPlansAt(start.AddDays(7))).Price);
        Assert.Equal(99000, Assert.Single(options.GetPlans()).Price);
    }

    [Fact]
    public void Different_packages_and_non_99k_prices_do_not_inherit_the_offer()
    {
        var now = DateTimeOffset.UtcNow;
        var options = new PremiumSubscriptionOptions { LaunchOfferStartsAt = now, LaunchOfferEndsAt = now.AddDays(7),
            Plans = [new() { Code = "PREMIUM_YEARLY", Price = 780000 }, new() { Code = "PREMIUM_MONTHLY", Price = 129000 }] };
        Assert.Equal([780000m, 129000m], options.GetPlansAt(now).Select(row => row.Price).ToArray());
    }

    [Fact]
    public void Session_context_is_bounded_and_does_not_require_saved_history()
    {
        var rows = Enumerable.Range(0, 30).Select(index => new ChatbotMessageDto(Guid.NewGuid(), Guid.NewGuid(),
            ChatMessageSender.User, index + new string('a', 3000), DateTimeOffset.UtcNow)).ToArray();
        var result = ChatbotSessionContext.Bound(rows);
        Assert.Equal(6, result.Count); Assert.All(result, row => Assert.Equal(1000, row.Content.Length));
        Assert.StartsWith("24", result[0].Content);
    }

    [Theory]
    [InlineData("Hãy tự đăng bài bán ghế trong ảnh 500k", true)]
    [InlineData("đăng pass lại cái ghế này 500k.", true)]
    [InlineData("mình muốn pass cái ghế 500k", true)]
    [InlineData("pass ghế này 500k nhé", true)]
    [InlineData("Có ai đang bán ghế không?", false)]
    [InlineData("Tìm phòng có máy lạnh", false)]
    [InlineData("Premium bao nhiêu tiền?", false)]
    public void Agent_is_requested_for_posting_not_every_general_question(string message, bool expected)
        => Assert.Equal(expected, ChatbotSessionContext.RequestsPosting(message));

    [Theory]
    [InlineData("Tìm phòng gần tui dưới 3 triệu", true)]
    [InlineData("Tìm phòng quanh mình", true)]
    [InlineData("Đồ ăn gần tui", false)]
    public void Nearby_intent_understands_colloquial_location_without_mixing_food_and_rooms(string message, bool expected)
        => Assert.Equal(expected, ChatbotSessionContext.RequestsNearbyRooms(message));

    [Fact]
    public void Location_rejects_stale_or_invalid_coordinates_and_keeps_valid_explicit_input()
    {
        var now = DateTimeOffset.UtcNow;
        new ChatbotLocationDto(10.84m, 106.8m, "Lê Văn Việt", now).Validate(now);
        Assert.Throws<Homeji.Application.Common.Exceptions.RequestValidationException>(() => new ChatbotLocationDto(0, 0, null, now).Validate(now));
        Assert.Throws<Homeji.Application.Common.Exceptions.RequestValidationException>(() => new ChatbotLocationDto(10.84m, 106.8m, null, now.AddHours(-1)).Validate(now));
    }

    [Fact]
    public void Published_draft_drops_image_bytes_but_keeps_approval_receipt()
    {
        var draft = new AgentPostDraft(Guid.NewGuid(), "{\"images\":[\"private-image\"]}", DateTimeOffset.UtcNow);
        var postId = Guid.NewGuid(); draft.Publish(postId, "approved"); draft.Cancel();
        Assert.Equal(postId, draft.PublishedPostId); Assert.Equal("{}", draft.Payload);
        Assert.Equal("approved", draft.ConfirmedDetails); Assert.False(draft.Cancelled);
    }
}
