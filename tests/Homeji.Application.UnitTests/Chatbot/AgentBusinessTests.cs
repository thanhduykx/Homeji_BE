using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.Chatbot;
using Homeji.Application.Common.Exceptions;
namespace Homeji.Application.UnitTests.Chatbot;

public sealed class AgentBusinessTests
{
    [Theory]
    [InlineData("tôi muốn đăng cho thuê phòng này giá 8triệu / tháng cọc 16 tr, ký ít nhất 12 tháng", "rental")]
    [InlineData("đăng tin pass phòng 3 triệu", "roomTransfer")]
    [InlineData("đăng tin tìm bạn ở ghép", "roommate")]
    [InlineData("đăng tin tìm trọ dưới 3 triệu", "wanted")]
    [InlineData("đăng bán cơm gà 35k", "food")]
    [InlineData("đăng bán iPhone 12 giá 5 triệu", "secondHand")]
    [InlineData("đăng bán bàn học dùng trong phòng trọ", "secondHand")]
    [InlineData("đăng bán computer giá 5 triệu", "secondHand")]
    [InlineData("soạn bài cho ghế trong ảnh giá 500k", "secondHand")]
    [InlineData("tạo tin tìm trọ 3 triệu", "wanted")]
    [InlineData("đăng pass lại cái ghế này 500k", "secondHand")]
    [InlineData("tìm phòng gần tôi", null)]
    [InlineData("tìm bạn ở ghép", null)]
    [InlineData("Có ai đang bán cơm không", null)]
    [InlineData("cách đăng tin cho thuê", null)]
    public void Posting_routes_match_the_business_and_do_not_capture_searches(string message, string? kind)
        => Assert.Equal(kind, AgentBusinessIntent.PostingKind(message));

    [Theory]
    [InlineData("Sửa giá phòng còn 7 triệu", false)]
    [InlineData("Cọc 16 triệu, diện tích 54m2", false)]
    [InlineData("Tìm phòng dưới 3 triệu", true)]
    [InlineData("Gợi ý món ăn quanh tôi", true)]
    public void Searching_a_new_topic_does_not_hijack_a_pending_draft(string message, bool search)
        => Assert.Equal(search, AgentBusinessIntent.IsDiscovery(message));

    [Fact]
    public void Rental_requires_explicit_fields_instead_of_defaulting_to_used_goods()
    {
        var details = Details with { Kind = "rental" };
        var error = Assert.Throws<RequestValidationException>(() => AgentPostBusinessRules.Validate(details, DateTimeOffset.UtcNow));
        Assert.Contains("deposit", error.Errors.Keys); Assert.Contains("area", error.Errors.Keys);
        AgentPostBusinessRules.Validate(details with { Deposit = 16000000, Area = 54, MaxOccupants = 2, AvailableSlots = 2, MinimumLeaseMonths = 12 }, DateTimeOffset.UtcNow);
    }
    [Fact]
    public void Pass_requires_explicit_owner_consent_and_contract_dates()
    {
        var details = Details with { Kind = "roomTransfer", Deposit = 0, Area = 26, MaxOccupants = 2, AvailableSlots = 1,
            TransferKind = 1, AvailableFrom = new DateOnly(2026, 12, 1), OriginalLeaseEndsOn = new DateOnly(2027, 12, 1), PassFee = 0, TransferReason = "Chuyển nơi làm việc" };
        Assert.Throws<RequestValidationException>(() => AgentPostBusinessRules.Validate(details, DateTimeOffset.UtcNow));
        AgentPostBusinessRules.Validate(details with { OwnerConsentConfirmed = true, OwnerConsentContact = "0901234567" }, DateTimeOffset.UtcNow);
    }
    [Fact]
    public void Food_requires_stock_unit_and_preparation_while_wanted_does_not_require_map_coordinates()
    {
        Assert.Throws<RequestValidationException>(() => AgentPostBusinessRules.Validate(Details with { Kind = "food" }, DateTimeOffset.UtcNow));
        AgentPostBusinessRules.Validate(Details with { Kind = "food", AvailableQuantity = 20, Unit = "phần", PreparationMinutes = 15 }, DateTimeOffset.UtcNow);
        AgentPostBusinessRules.Validate(Details with { Kind = "wanted", Latitude = 0, Longitude = 0, MaxOccupants = 2, AvailableFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)) }, DateTimeOffset.UtcNow);
    }
    private static AgentPostDetails Details => new("Tin kiểm thử", "Nội dung do người dùng cung cấp", 8000000, "", "", "Lê Văn Việt", 10.84m, 106.8m);
}
