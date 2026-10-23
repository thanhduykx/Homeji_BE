using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.IRepositories.Chatbot;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IServices.AI;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.Services.Chatbot;
using Homeji.Application.Services.Common;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotServiceTests
{
    private static readonly Guid UserId = Guid.Parse("8e996f4c-ec40-4b5e-b66b-9f33c4f29b63");
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("hi")]
    [InlineData("Cách thanh toán bằng PayOS như thế nào?")]
    public async Task KnownSupportQuestion_CallsGeminiAndDisclosesProviderFailure(string message)
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new CountingUnavailableClient();
        var reply = await CreateService(repository, ai, historyStorageEnabled: false)
            .SendMessageAsync(new(null, message));
        Assert.Contains("không khả dụng", reply.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, ai.Calls);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData("hi")]
    [InlineData("Cách thanh toán bằng PayOS như thế nào?")]
    public async Task KnownSupportQuestion_ReturnsActualGeminiReply(string message)
    {
        var ai = new SuccessfulClient();
        var response = await CreateService(new InMemoryChatConversationRepository(), ai, historyStorageEnabled: false)
            .SendMessageAsync(new(null, message));
        Assert.Equal("Câu trả lời thực từ Gemini.", response.AssistantMessage.Content);
        Assert.Equal(1, ai.Calls);
    }

    private sealed class SuccessfulClient : IChatbotAiClient
    {
        public int Calls { get; private set; }
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string latestUserMessage, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult("Câu trả lời thực từ Gemini.");
        }
    }

    [Theory]
    [InlineData("CÓ GÌ ĂN GIỚI THIỆU ĐI")]
    [InlineData("GIỚI THIỆU ĐỒ ĂN NGON GẦN TUI")]
    public async Task FoodDiscovery_NeverReturnsRentalResultsOrAnUngroundedProviderGuide(string message)
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new SuccessfulClient();
        var reply = await CreateService(repository, ai, new GroundedSearchStub()).SendMessageAsync(new(null, message));
        Assert.Null(reply.SearchUpdate);
        Assert.Null(reply.RentalDecision);
        Assert.NotNull(reply.FoodDiscovery);
        Assert.Equal(0, ai.Calls);
        Assert.Equal(0, repository.SaveCount);
    }

    private sealed class CountingUnavailableClient : IChatbotAiClient
    {
        public int Calls { get; private set; }
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string latestUserMessage, CancellationToken cancellationToken = default)
        {
            Calls++;
            throw new ExternalServiceUnavailableException("Gemini", "Unavailable");
        }
    }

    [Fact]
    public async Task GetPopupConfigAsync_UsesExactHomejiBrandName()
    {
        var service = CreateService(new InMemoryChatConversationRepository(), new UnavailableChatbotAiClient());

        var config = await service.GetPopupConfigAsync();

        Assert.Equal("Homeji", config.Title);
        Assert.Contains("trợ lý Homeji", config.Greeting, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendMessageAsync_WhenAiIsUnavailable_ReturnsSafeInteractiveFallback()
    {
        var conversations = new InMemoryChatConversationRepository();
        var service = CreateService(conversations, new UnavailableChatbotAiClient());

        var reply = await service.SendMessageAsync(
            new SendChatbotMessageDto(null, "Mình muốn đặt đồ ăn"));

        Assert.Contains("xác nhận", reply.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tổng tiền", reply.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(reply.Actions, action => action.Target == "marketplace:food");
        Assert.Equal(0, conversations.SaveCount);
        Assert.Null(conversations.Conversation);
    }

    private static ChatbotService CreateService(
        InMemoryChatConversationRepository conversations,
        IChatbotAiClient aiClient,
        IAiSearchService? search = null,
        bool historyStorageEnabled = true,
        IRentalDecisionService? decision = null)
    {
        return new ChatbotService(
            new UserContext(new StubCurrentUser(UserId), new StubUserProfileRepository()),
            conversations,
            aiClient,
            search ?? new StubAiSearchService(),
            decision ?? new StubRentalDecisionService(),
            new StubFoodService(),
            Options.Create(new ChatbotOptions { HistoryStorageEnabled = historyStorageEnabled }),
            new StubTimeProvider());
    }

    [Theory]
    [InlineData("cost", "Tính tiền tháng", "Chưa đủ thông tin")]
    [InlineData("details", "Xem thông tin", "Diện tích theo tin")]
    [InlineData("questions", "Chuẩn bị câu hỏi", "Mình chưa gửi tin nhắn")]
    [InlineData(null, "Phòng này tổng tiền tháng bao nhiêu?", "Chưa đủ thông tin")]
    [InlineData(null, "Tien dien nuoc cua phong nay?", "Chưa đủ thông tin")]
    [InlineData(null, "Soạn câu hỏi cho chủ phòng", "Mình chưa gửi tin nhắn")]
    [InlineData(null, "Phong nay con trong khong?", "Cần hỏi chủ tin")]
    public async Task SelectedRoom_UsesAuthorizedDecisionFactsWithoutSearchOrProvider(string? task, string message, string expected)
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new SuccessfulClient();
        var decision = new StubRentalDecisionService();
        var postId = Guid.NewGuid();
        var reply = await CreateService(repository, ai, decision: decision)
            .SendMessageAsync(new(null, message, RentalContext: new(postId, task)));

        Assert.Equal(new[] { postId }, decision.LastRequest!.PostIds);
        Assert.Null(decision.LastRequest.Scenario);
        Assert.Null(decision.LastRequest.Destination);
        Assert.Same(decision.Response, reply.RentalDecision);
        Assert.Null(reply.SearchUpdate);
        Assert.Empty(reply.Actions);
        Assert.Contains(expected, reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("đã xác minh", reply.AssistantMessage.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, ai.Calls);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData("Tìm phòng dưới 2 triệu")]
    [InlineData("Lọc phòng có bếp")]
    [InlineData("Gợi ý phòng khác")]
    public async Task ExplicitNewSearch_DoesNotUseSelectedRoomEvenWithAContextTask(string message)
    {
        var decision = new StubRentalDecisionService();
        var reply = await CreateService(new(), new SuccessfulClient(), new GroundedSearchStub(), decision: decision)
            .SendMessageAsync(new(null, message, RentalContext: new(Guid.NewGuid(), "cost")));

        Assert.NotNull(reply.SearchUpdate);
        Assert.Null(reply.RentalDecision);
        Assert.Equal(0, decision.Calls);
    }

    [Theory]
    [InlineData("Cách thanh toán bằng PayOS?")]
    [InlineData("Đổi số điện thoại tài khoản như thế nào?")]
    public async Task UnrelatedSupportQuestion_KeepsItsExistingBehaviorWhenARoomIsSelected(string message)
    {
        var ai = new SuccessfulClient();
        var decision = new StubRentalDecisionService();
        var reply = await CreateService(new(), ai, decision: decision)
            .SendMessageAsync(new(null, message, RentalContext: new(Guid.NewGuid())));

        Assert.Equal("Câu trả lời thực từ Gemini.", reply.AssistantMessage.Content);
        Assert.Null(reply.RentalDecision);
        Assert.Equal(1, ai.Calls);
        Assert.Equal(0, decision.Calls);
    }

    [Fact]
    public async Task UnavailableSelectedRoom_ReturnsSafeAnswerWithoutFallingThroughToProvider()
    {
        var postId = Guid.NewGuid();
        var ai = new SuccessfulClient();
        var decision = new StubRentalDecisionService { Response = new([], [postId], "Unavailable") };
        var reply = await CreateService(new(), ai, decision: decision)
            .SendMessageAsync(new(null, "Phòng này tổng bao nhiêu?", RentalContext: new(postId, "cost")));

        Assert.Same(decision.Response, reply.RentalDecision);
        Assert.Empty(reply.RentalDecision!.Posts);
        Assert.Contains("không còn khả dụng", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.Equal(0, ai.Calls);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("send-message", false)]
    [InlineData("COST", false)]
    public async Task InvalidRentalContext_IsRejectedBeforeAnyProviderOrPersistence(string? task, bool emptyId)
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new SuccessfulClient();
        var decision = new StubRentalDecisionService();
        var service = CreateService(repository, ai, decision: decision);

        await Assert.ThrowsAsync<RequestValidationException>(() => service.SendMessageAsync(
            new(null, "Phòng này", RentalContext: new(emptyId ? Guid.Empty : Guid.NewGuid(), task))));
        Assert.Equal(0, ai.Calls);
        Assert.Equal(0, decision.Calls);
        Assert.Null(repository.Conversation);
    }

    [Fact]
    public async Task PricePerPerson_IsNotPresentedAsARoomTotal()
    {
        var decision = new StubRentalDecisionService();
        var item = decision.Response.Posts.Single();
        decision.Response = decision.Response with
        {
            Posts = [item with { Cost = item.Cost with { PriceBasis = "person", KnownMonthlySubtotal = null } }],
        };
        var reply = await CreateService(new(), new SuccessfulClient(), decision: decision)
            .SendMessageAsync(new(null, "Tính tiền tháng", RentalContext: new(Guid.NewGuid(), "cost")));

        Assert.Contains("đồng/người/tháng", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("đồng/phòng/tháng", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.Contains("Chưa đủ thông tin", reply.AssistantMessage.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("compare", "So sánh các lựa chọn này", "Diện tích theo tin")]
    [InlineData("cost", "Tính tiền tháng", "Chưa đủ thông tin")]
    [InlineData("questions", "Chuẩn bị câu hỏi", "Mình chưa gửi tin nhắn")]
    [InlineData(null, "Hai lựa chọn này cần hỏi chủ tin những gì?", "Mình chưa gửi tin nhắn")]
    [InlineData(null, "Chi phí của ba phòng này?", "Chưa đủ thông tin")]
    public async Task SelectedChoices_UseOneAuthorizedScenarioWithoutProviderOrPersistence(string? task, string message, string expected)
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new SuccessfulClient();
        var decision = new StubRentalDecisionService();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var scenario = new RentalCostScenarioDto(Occupants: 2, ElectricityKwh: 120, WaterM3: 6,
            ElectricityUnit: "kwh", WaterUnit: "m3", InternetUnit: "month");

        var reply = await CreateService(repository, ai, decision: decision).SendMessageAsync(
            new(null, message, RentalSelection: new(ids, task, scenario)));

        Assert.Equal(ids, decision.LastRequest!.PostIds);
        Assert.Same(scenario, decision.LastRequest.Scenario);
        Assert.Null(decision.LastRequest.Destination);
        Assert.Same(decision.Response, reply.RentalDecision);
        Assert.Contains(expected, reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Địa chỉ riêng tư", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.Null(reply.SearchUpdate);
        Assert.Empty(reply.Actions);
        Assert.Equal(0, ai.Calls);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task ExplicitNewSearch_DoesNotUseSelectedChoicesOrTheirScenario()
    {
        var decision = new StubRentalDecisionService();
        var reply = await CreateService(new(), new SuccessfulClient(), new GroundedSearchStub(), decision: decision)
            .SendMessageAsync(new(null, "Tìm phòng dưới 2 triệu", RentalSelection:
                new([Guid.NewGuid(), Guid.NewGuid()], "compare", new(Occupants: 2))));
        Assert.NotNull(reply.SearchUpdate);
        Assert.Null(reply.RentalDecision);
        Assert.Equal(0, decision.Calls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task InvalidSelection_IsRejectedBeforeReadingFactsCallingProviderOrSaving(int invalidCase)
    {
        var id = Guid.NewGuid();
        IReadOnlyCollection<Guid>? ids = invalidCase switch
        {
            0 => null, 1 => [], 2 => [id], 3 => [id, id], 4 => [id, Guid.Empty],
            5 => [id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()], _ => [id, Guid.NewGuid()],
        };
        var repository = new InMemoryChatConversationRepository();
        var ai = new SuccessfulClient();
        var decision = new StubRentalDecisionService();
        await Assert.ThrowsAsync<RequestValidationException>(() => CreateService(repository, ai, decision: decision)
            .SendMessageAsync(new(null, "Hai lựa chọn này", RentalContext: invalidCase == 7 ? new(id) : null,
                RentalSelection: new(ids, invalidCase == 6 ? "send-message" : "compare"))));
        Assert.Equal(0, decision.Calls);
        Assert.Equal(0, ai.Calls);
        Assert.Null(repository.Conversation);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnavailableChoices_DiscloseIncompleteCoverageWithoutGuessing(bool allUnavailable)
    {
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var decision = new StubRentalDecisionService();
        decision.Response = decision.Response with
        {
            Posts = allUnavailable ? [] : decision.Response.Posts,
            UnavailablePostIds = allUnavailable ? ids : [ids[1]],
        };
        var ai = new SuccessfulClient();
        var reply = await CreateService(new(), ai, decision: decision).SendMessageAsync(
            new(null, "Hai lựa chọn này", RentalSelection: new(ids)));
        Assert.Contains(allUnavailable ? "không còn khả dụng" : "chưa thể coi đây là so sánh đủ", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.Equal(0, ai.Calls);
    }

    private sealed class StubRentalDecisionService : IRentalDecisionService
    {
        public int Calls { get; private set; }
        public RentalDecisionRequestDto? LastRequest { get; private set; }
        public RentalDecisionResponseDto Response { get; set; } = CreateResponse();

        public Task<RentalDecisionResponseDto> CompareAsync(RentalDecisionRequestDto request, CancellationToken cancellationToken)
        {
            Calls++;
            LastRequest = request;
            return Task.FromResult(Response);
        }

        public Task<RentalDraftPreviewDto> PreviewDraftAsync(RentalDraftPreviewRequestDto request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AdminAssistantSummaryDto> GetAdminSummaryAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        private static RentalDecisionResponseDto CreateResponse()
        {
            var post = RentalPost.CreateDraft(Guid.NewGuid(), RentalPostType.VacantRoom, UtcNow);
            post.UpdateDetails(RentalPostType.VacantRoom, "Phòng thật", "Thông tin chủ tin", 3_000_000,
                3_000_000, 25, "Địa chỉ riêng tư không đưa vào câu trả lời", 10.84m, 106.80m, [], UtcNow);
            return new([new(RentalPostMapper.ToDto(post),
                new(post.Id, 3_000_000, null, null, ["Điện: chưa xác nhận đơn giá/đơn vị"],
                    ["Điện tính theo kWh, tháng hay đầu người?"], "ownerListing + userScenario", UtcNow, "room"), null)], [], "Theo tin đăng");
        }
    }

    [Fact]
    public async Task SessionCriteria_CanBeEditedWithoutPersistingAnyConversation()
    {
        var repository = new InMemoryChatConversationRepository();
        var service = CreateService(repository, new UnavailableChatbotAiClient(), new GroundedSearchStub(), historyStorageEnabled: false);
        var initial = await service.SendMessageAsync(new(null, "Phòng dưới 4tr có bếp có máy lạnh"));
        var edited = await service.SendMessageAsync(new(null, "Không cần máy lạnh", PreviousCriteria: initial.SearchUpdate!.Criteria));
        Assert.Equal(4_000_000, edited.SearchUpdate!.Criteria.PriceMax);
        Assert.Contains("KITCHEN", edited.SearchUpdate.Criteria.RequiredAmenities);
        Assert.DoesNotContain("AIR_CONDITIONER", edited.SearchUpdate.Criteria.RequiredAmenities);
        Assert.Null(repository.Conversation); Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task StorageRequiresExplicitConsentAndAnEnabledServerPolicy()
    {
        var repository = new InMemoryChatConversationRepository();
        var service = CreateService(repository, new UnavailableChatbotAiClient(), historyStorageEnabled: false);
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.SendMessageAsync(new(null, "Xin chào", SaveHistory: true)));
        await Assert.ThrowsAsync<RequestValidationException>(() => service.SendMessageAsync(new(Guid.NewGuid(), "Xin chào")));
        Assert.Null(repository.Conversation); Assert.Equal(0, repository.SaveCount);
    }

    public static IEnumerable<object[]> ConversationCases() => Enumerable.Range(0, 20).Select(index => new object[] { index });

    [Theory]
    [MemberData(nameof(ConversationCases))]
    public async Task TwentyIndependentConversations_EditStructuredPreferencesWithoutReintroducingRemovedConditions(int index)
    {
        var repository = new InMemoryChatConversationRepository();
        var service = CreateService(repository, new UnavailableChatbotAiClient(), new GroundedSearchStub());
        var initial = await service.SendMessageAsync(new(null, $"Phòng dưới {index % 3 + 3}tr có bếp có máy lạnh cho 2 người", SaveHistory: true));
        var edited = await service.SendMessageAsync(new(initial.ConversationId, "Không cần máy lạnh, phòng dưới 2tr", SaveHistory: true));
        Assert.NotNull(edited.SearchUpdate);
        Assert.Equal(2_000_000, edited.SearchUpdate.Criteria.PriceMax);
        Assert.Contains("KITCHEN", edited.SearchUpdate.Criteria.RequiredAmenities);
        Assert.DoesNotContain("AIR_CONDITIONER", edited.SearchUpdate.Criteria.RequiredAmenities);
        var optional = await service.SendMessageAsync(new(initial.ConversationId, "Ưu tiên bếp", SaveHistory: true));
        Assert.Contains("KITCHEN", optional.SearchUpdate!.Criteria.Criteria);
        Assert.DoesNotContain("KITCHEN", optional.SearchUpdate.Criteria.RequiredAmenities);
        var cleared = await service.SendMessageAsync(new(initial.ConversationId, "Bỏ ngân sách, không cần bếp", SaveHistory: true));
        Assert.Null(cleared.SearchUpdate!.Criteria.PriceMax);
        Assert.Empty(cleared.SearchUpdate.Criteria.RequiredAmenities);
        Assert.Empty(cleared.SearchUpdate.Criteria.Criteria);
        Assert.Equal(2, cleared.SearchUpdate.Criteria.Occupants);
    }

    private sealed class GroundedSearchStub : IAiSearchService
    {
        public Task<AiParsedSearchCriteriaDto> ParseSearchAsync(AiParseSearchRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiHighlightResponseDto> HighlightRentalPostsAsync(AiHighlightRequestDto request, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AiHighlightResponseDto(Homeji.Application.Services.AI.RentalSearchIntent.Apply(request.Text!, request.PreviousCriteria), [], "Test", null, null, null));
    }

    [Fact]
    public async Task DeleteConversation_RequiresOwnershipAndRemovesMessagesAndPreferencesTogether()
    {
        var repository = new InMemoryChatConversationRepository();
        var other = ChatConversation.Create(Guid.NewGuid(), "Other user's conversation", UtcNow);
        await repository.AddAsync(other);
        var service = CreateService(repository, new UnavailableChatbotAiClient());
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => service.DeleteConversationAsync(other.Id));
        Assert.Same(other, repository.Conversation);
        var own = ChatConversation.Create(UserId, "My conversation", UtcNow);
        own.UpdateSearchCriteria("{}"); own.AddUserMessage("Phòng dưới 4 triệu", UtcNow);
        await repository.AddAsync(own);
        await service.DeleteConversationAsync(own.Id);
        Assert.Null(repository.Conversation); Assert.Equal(1, repository.SaveCount);
    }

    private sealed record StubCurrentUser(Guid? UserId) : ICurrentUser;

    private sealed class StubFoodService : IChatbotFoodService
    {
        public Task<ChatbotFoodResponseDto> SearchAsync(string message, ChatbotFoodContextDto? origin, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatbotFoodResponseDto("empty", "Chưa có món đang bán.", []));
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class UnavailableChatbotAiClient : IChatbotAiClient
    {
        public Task<string> GenerateReplyAsync(
            IReadOnlyCollection<ChatbotMessageDto> conversationMessages,
            string latestUserMessage,
            CancellationToken cancellationToken = default)
        {
            throw new ExternalServiceUnavailableException(
                "Gemini",
                "Chatbot tạm thời không khả dụng.");
        }
    }

    private sealed class StubAiSearchService : IAiSearchService
    {
        public Task<AiParsedSearchCriteriaDto> ParseSearchAsync(
            AiParseSearchRequestDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AiHighlightResponseDto> HighlightRentalPostsAsync(
            AiHighlightRequestDto request,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class InMemoryChatConversationRepository : IChatConversationRepository
    {
        public void Remove(ChatConversation conversation) { if (Conversation == conversation) Conversation = null; }
        public ChatConversation? Conversation { get; private set; }

        public int SaveCount { get; private set; }

        public Task<ChatConversation?> GetByIdWithMessagesAsync(
            Guid conversationId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Conversation?.Id == conversationId ? Conversation : null);
        }

        public Task<IReadOnlyList<ChatConversation>> GetByUserIdAsync(
            Guid userId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ChatConversation> result = Conversation?.UserId == userId
                ? [Conversation]
                : [];
            return Task.FromResult(result);
        }

        public Task AddAsync(
            ChatConversation conversation,
            CancellationToken cancellationToken = default)
        {
            Conversation = conversation;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUserProfileRepository : IUserProfileRepository
    {
        private readonly UserProfile _profile = UserProfile.Create(UserId, "Homeji User", UtcNow);

        public Task<UserProfile?> GetByIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<UserProfile?>(userId == UserId ? _profile : null);
        }

        public Task<UserProfile> UpsertAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default) => Task.FromResult(profile);

        public Task<UserProfile> SaveAsync(
            UserProfile profile,
            CancellationToken cancellationToken = default) => Task.FromResult(profile);

        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<UserProfile> result = userIds.Contains(UserId) ? [_profile] : [];
            return Task.FromResult(result);
        }

        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Guid>>([UserId]);
        }

        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(
            string address,
            decimal price,
            Guid excludedUserId,
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<UserProfile>>([]);
        }
    }
}
