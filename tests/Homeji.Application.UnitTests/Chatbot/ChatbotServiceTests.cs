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
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Normal_chat_gives_guidance_without_preparing_a_draft_and_only_upsells_basic_users(bool media, bool premium)
    {
        var agent = new CapturingAgent();
        var ai = new GuidanceClient();
        var reply = await CreateService(new(), ai, posting: agent, subscriptions: new SubscriptionStub(premium)).SendMessageAsync(
            new(null, "tôi muốn pass lại cái ghế này với giá 500k", Attachments: media ? [new("image/png", "AA==")] : null,
                AgentDraftId: Guid.NewGuid()));
        Assert.Equal(0, agent.Calls);
        Assert.Equal(1, media ? ai.MediaCalls : ai.TextCalls);
        Assert.StartsWith("Bạn có thể đăng chiếc ghế giá 500.000đ", reply.AssistantMessage.Content);
        Assert.Null(reply.AgentDraft); Assert.False(reply.AgentRequested);
        Assert.Equal(!premium, reply.UpgradeRequired);
        Assert.Single(reply.Actions);
        if (premium) Assert.Contains("chuyển sang Agent", reply.AssistantMessage.Content);
        else Assert.Contains("nâng cấp Premium", reply.AssistantMessage.Content);
    }

    private sealed class GuidanceClient : IChatbotAiClient
    {
        public int TextCalls { get; private set; }
        public int MediaCalls { get; private set; }
        private const string Reply = "Bạn có thể đăng chiếc ghế giá 500.000đ trên Chợ đồ. Chụp rõ ghế, ghi tình trạng, kích thước và nơi nhận; kiểm tra thông tin rồi tự đăng.";
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> history, string message, CancellationToken cancellationToken = default)
        { TextCalls++; return Task.FromResult(Reply); }
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> history, string message, IReadOnlyCollection<ChatbotMediaContent> files, CancellationToken cancellationToken = default)
        { MediaCalls++; Assert.Single(files); return Task.FromResult(Reply); }
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    private sealed class SubscriptionStub(bool premium) : Homeji.Application.IRepositories.Subscriptions.IUserSubscriptionRepository
    {
        public Task<UserSubscription?> GetActivePremiumAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default)
            => Task.FromResult(premium ? UserSubscription.CreatePremium(id, "premium", "Premium", null, now.AddDays(-1), 30, now.AddDays(-1)) : null);
        public Task<UserSubscription?> GetPremiumTailAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<Guid, UserSubscription>> GetActivePremiumByUserIdsAsync(IReadOnlyCollection<Guid> ids, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserSubscription?> GetByPaymentTransactionIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(UserSubscription subscription, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("đăng pass lại cái ghế này 500k.")]
    [InlineData("đăng pas lại cái ghế giá 500k")]
    public async Task Exact_pass_request_with_photo_routes_to_the_posting_tool_in_agent_mode(string requestText)
    {
        var ai = new MediaClient(); var agent = new CapturingAgent();
        var reply = await CreateService(new(), ai, posting: agent).SendMessageAsync(new(null,
            requestText, Attachments: [new("image/png", "AA==")],
            Location: new(10.84m, 106.8m, "Lê Văn Việt", UtcNow), PostingAgent: true));
        Assert.Equal(1, agent.Calls); Assert.Equal(0, ai.MediaCalls);
        Assert.True(reply.AgentRequested); Assert.Equal("Bản nháp đã soạn", reply.AssistantMessage.Content);
        Assert.Equal("Lê Văn Việt", agent.Location!.Address);
    }
    [Fact]
    public async Task Agent_mode_general_question_does_not_generate_a_sale_draft()
    {
        var ai = new SuccessfulClient(); var agent = new CapturingAgent();
        await CreateService(new(), ai, posting: agent).SendMessageAsync(new(null, "Premium bao nhiêu tiền?", PostingAgent: true));
        Assert.Equal(0, agent.Calls);
    }
    [Fact]
    public async Task Agent_mode_roommate_search_does_not_generate_a_post()
    {
        var ai = new MediaClient(); var agent = new CapturingAgent();
        var reply = await CreateService(new(), ai, posting: agent).SendMessageAsync(new(null, "tìm bạn ở ghép", PostingAgent: true));
        Assert.Equal(0, agent.Calls); Assert.Contains(reply.Actions, action => action.Target == "invitations");
    }
    [Fact]
    public async Task Roommate_search_returns_actual_opted_in_directory_profiles_without_posting_or_invitations()
    {
        var agent = new CapturingAgent(); var directory = new DirectoryStub();
        var reply = await CreateService(new(), new SuccessfulClient(), posting: agent, roommates: directory).SendMessageAsync(new(null, "tìm bạn ở ghép", PostingAgent: true));
        Assert.Equal(0, agent.Calls); Assert.Equal(directory.UserId, Assert.Single(reply.RoommateCandidates!).UserId);
    }
    private sealed class DirectoryStub : Homeji.Application.IServices.Roommates.IRoommateDirectoryService
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Task<Homeji.Application.DTOs.Roommates.RoommateDirectoryPageDto> SearchAsync(RoommateIntent? intent, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
            => Task.FromResult(new Homeji.Application.DTOs.Roommates.RoommateDirectoryPageDto([new(UserId, "Người tìm ở ghép", null, null, "Thủ Đức", 3000000,
                (SleepHabit)0, (PetPreference)0, (SmokingPreference)0, RoommateIntent.SeekingAccommodation, "Tìm bạn cùng thuê", null)], 1, page, pageSize));
        public Task<Homeji.Application.DTOs.Roommates.RoommateProfileDto> GetMineAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Homeji.Application.DTOs.Roommates.RoommateProfileDto> UpdateMineAsync(Homeji.Application.DTOs.Roommates.RoommateProfileDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    [Theory]
    [InlineData("1. tình trạng mới, kick thước 500mm*200mm, chất liệu da")]
    [InlineData("1")]
    public async Task Posting_followup_recovers_the_recent_user_task_without_trusting_assistant_prose(string message)
    {
        var agent = new CapturingAgent();
        var rows = new ChatbotMessageDto[] {
            new(Guid.NewGuid(), Guid.Empty, ChatMessageSender.User, "đăng pas lại cái ghế giá 500k", UtcNow),
            new(Guid.NewGuid(), Guid.Empty, ChatMessageSender.Assistant, "Tin của bạn đã được gửi vào hệ thống kiểm duyệt", UtcNow),
            new(Guid.NewGuid(), Guid.Empty, ChatMessageSender.User, "tình trạng mới, kick thước 500mm*200mm, chất liệu da", UtcNow),
            new(Guid.NewGuid(), Guid.Empty, ChatMessageSender.Assistant, "Thông tin bổ sung đã được ghi nhận", UtcNow),
        };
        var reply = await CreateService(new(), new MediaClient(), posting: agent).SendMessageAsync(
            new(null, message, SessionMessages: rows, Attachments: [new("image/png", "AA==")], PostingAgent: true));
        Assert.Equal(1, agent.Calls); Assert.True(reply.AgentRequested);
        Assert.Contains("500k", agent.Message); Assert.Contains("500mm*200mm", agent.Message);
        Assert.Contains("chất liệu da", agent.Message); Assert.Equal(1, agent.ImageCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Free_form_generation_cannot_claim_a_post_was_published(bool media)
    {
        var response = await CreateService(new(), new ClaimingClient()).SendMessageAsync(new(null, "1",
            Attachments: media ? [new("image/png", "AA==")] : null));
        Assert.Contains("chưa thực hiện thao tác đăng bài", response.AssistantMessage.Content);
        Assert.Null(response.AgentDraft); Assert.False(response.AgentRequested);
    }

    [Fact]
    public async Task Pending_draft_does_not_capture_a_general_premium_question()
    {
        var agent = new CapturingAgent(); var ai = new SuccessfulClient();
        await CreateService(new(), ai, posting: agent).SendMessageAsync(new(null, "Premium bao nhiêu tiền?", AgentDraftId: Guid.NewGuid(), PostingAgent: true));
        Assert.Equal(0, agent.Calls); Assert.Equal(1, ai.Calls);
    }

    private sealed class ClaimingClient : IChatbotAiClient
    {
        private const string Claim = "Tin của bạn đã được gửi vào hệ thống kiểm duyệt.";
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, CancellationToken cancellationToken = default) => Task.FromResult(Claim);
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> files, CancellationToken cancellationToken = default) => Task.FromResult(Claim);
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingAgent : IChatbotPostingAgent
    {
        public int Calls { get; private set; }
        public string Message { get; private set; } = "";
        public int ImageCount { get; private set; }
        public ChatbotLocationDto? Location { get; private set; }
        public Task<AgentPostResultDto> PrepareAsync(string message, IReadOnlyCollection<ChatbotMediaContent> media, Guid? previousDraftId, ChatbotLocationDto? location = null, CancellationToken cancellationToken = default)
        { Calls++; Message = message; ImageCount = media.Count; Location = location; return Task.FromResult(new AgentPostResultDto(null, false, "Bản nháp đã soạn")); }
        public Task<Guid> ConfirmAsync(Guid draftId, AgentPostDetails details, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task CancelAsync(Guid draftId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
    private static readonly Guid UserId = Guid.Parse("8e996f4c-ec40-4b5e-b66b-9f33c4f29b63");
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MediaUsesProviderWithoutPretendingToSearchOrPersistFiles()
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new MediaClient();
        var response = await CreateService(repository, ai, historyStorageEnabled: false).SendMessageAsync(
            new(null, "Tìm phòng giống ảnh", Attachments: [new("image/png", "AA==")]));
        Assert.Equal("Đã phân tích tệp.", response.AssistantMessage.Content);
        Assert.Equal(1, ai.MediaCalls);
        Assert.Null(response.SearchUpdate);
        Assert.Null(response.FoodDiscovery);
        Assert.Null(repository.Conversation);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task MediaProviderFailurePropagatesForRetryInsteadOfReturningTextOnlyFallback()
    {
        var repository = new InMemoryChatConversationRepository();
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => CreateService(repository, new MediaClient(fail: true))
            .SendMessageAsync(new(null, null, Attachments: [new("image/png", "AA==")])));
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task TranscriptionReturnsDraftWithoutCreatingConversationAndRejectsImages()
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new MediaClient();
        var service = CreateService(repository, ai);
        Assert.Equal("Tìm phòng dưới ba triệu.", await service.TranscribeAsync(new(ChatbotMediaValidationTests.CreateAudio())));
        Assert.Null(repository.Conversation);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.TranscribeAsync(new(new("image/png", "AA=="))));
        Assert.Equal(1, ai.TranscriptionCalls);
    }

    private sealed class MediaClient(bool fail = false) : IChatbotAiClient
    {
        public int MediaCalls { get; private set; }
        public int TranscriptionCalls { get; private set; }
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Media must not use text-only generation.");
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> attachments, CancellationToken cancellationToken = default)
        {
            MediaCalls++;
            if (fail) throw new ExternalServiceUnavailableException("Gemini", "Unavailable");
            Assert.Single(attachments);
            return Task.FromResult("Đã phân tích tệp.");
        }
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default)
        {
            TranscriptionCalls++;
            return Task.FromResult("Tìm phòng dưới ba triệu.");
        }
    }

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
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> attachments, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

    [Theory]
    [InlineData("gần tui")]
    [InlineData("dưới 30k")]
    [InlineData("30k")]
    [InlineData("bỏ ngân sách")]
    public async Task ShortFoodFollowupDoesNotBecomeRentalSearchOrASelectedRoomTask(string message)
    {
        var repository = new InMemoryChatConversationRepository();
        var ai = new SuccessfulClient(); var decision = new StubRentalDecisionService();
        var reply = await CreateService(repository, ai, new GroundedSearchStub(), decision: decision).SendMessageAsync(
            new(null, message, RentalContext: new(Guid.NewGuid(), "cost"),
                PreviousFoodCriteria: new("cơm gà", 40_000)));
        Assert.NotNull(reply.FoodDiscovery); Assert.Null(reply.RentalDecision); Assert.Null(reply.SearchUpdate);
        Assert.Equal(0, ai.Calls); Assert.Equal(0, decision.Calls); Assert.Equal(0, repository.SaveCount);
    }

    private sealed class CountingUnavailableClient : IChatbotAiClient
    {
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> attachments, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
        IRentalDecisionService? decision = null, IChatbotPostingAgent? posting = null, Homeji.Application.IServices.Roommates.IRoommateDirectoryService? roommates = null, Homeji.Application.IRepositories.Subscriptions.IUserSubscriptionRepository? subscriptions = null,
        Homeji.Application.IServices.RentalPosts.IRentalSourceListingService? sourceListings = null)
    {
        return new ChatbotService(
            new UserContext(new StubCurrentUser(UserId), new StubUserProfileRepository()),
            conversations,
            aiClient,
            search ?? new StubAiSearchService(),
            decision ?? new StubRentalDecisionService(),
            new StubFoodService(),
            Options.Create(new ChatbotOptions { HistoryStorageEnabled = historyStorageEnabled }),
            new StubTimeProvider(), posting, roommates: roommates, subscriptions: subscriptions, sourceListings: sourceListings);
    }

    private static readonly AiParsedSearchCriteriaDto ListedCriteria = new("Thủ Đức", null, null, 3_000_000, null, null, []);

    private sealed class StubSourceListings(int total = 12) : Homeji.Application.IServices.RentalPosts.IRentalSourceListingService
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<Homeji.Application.DTOs.RentalPosts.RentalSourceListingDto>> SearchAsync(Homeji.Application.DTOs.RentalPosts.RentalSourceSearchDto query, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Homeji.Application.DTOs.RentalPosts.RentalSourceListingDto> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Homeji.Application.DTOs.RentalPosts.RentalSourceContactDto> GetContactAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ChatbotSourceMatchDto> MatchAsync(AiParsedSearchCriteriaDto criteria, int limit, CancellationToken cancellationToken)
        {
            Calls++;
            var rooms = Enumerable.Range(1, Math.Min(limit, total)).Select(index =>
                new ChatbotSourceRoomDto(Guid.NewGuid(), $"Phòng nguồn {index}", "Linh Trung", 2_000_000 + index, 20, ["Máy lạnh"], "Cọc 1 tháng", "Mô tả ngắn")).ToArray();
            return Task.FromResult(new ChatbotSourceMatchDto(rooms, total));
        }
    }

    private sealed class FactsClient : IChatbotAiClient
    {
        public string? Facts { get; private set; }
        public int PlainCalls { get; private set; }
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> attachments, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> GenerateReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, CancellationToken cancellationToken = default) { PlainCalls++; return Task.FromResult("Trả lời chung"); }
        public Task<string> GenerateGroundedReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, string listingFacts, CancellationToken cancellationToken = default) { Facts = listingFacts; return Task.FromResult("Phòng 2 có máy lạnh theo mô tả."); }
    }

    private sealed class ParserDownSearch : IAiSearchService
    {
        public Task<AiParsedSearchCriteriaDto> ParseSearchAsync(AiParseSearchRequestDto request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AiHighlightResponseDto> HighlightRentalPostsAsync(AiHighlightRequestDto request, CancellationToken cancellationToken = default) =>
            request.Criteria is null
                ? throw new ExternalDependencyException("Gemini tạm thời không khả dụng.")
                : Task.FromResult(new AiHighlightResponseDto(request.Criteria, [], "Test", null, null, null));
    }

    [Fact]
    public async Task Room_search_includes_source_listings_and_offers_the_full_list()
    {
        var reply = await CreateService(new(), new SuccessfulClient(), new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, "Tìm phòng dưới 3 triệu"));

        Assert.Equal(12, reply.SourceRooms!.Total);
        Assert.Equal(5, reply.SourceRooms.Rooms.Count);
        Assert.Contains("Có 12 phòng", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.Contains(reply.Actions, action => action.Id == "all-rooms");
    }

    [Fact]
    public async Task All_matches_shown_does_not_offer_the_full_list()
    {
        var reply = await CreateService(new(), new SuccessfulClient(), new GroundedSearchStub(), sourceListings: new StubSourceListings(2))
            .SendMessageAsync(new(null, "Tìm phòng dưới 3 triệu"));

        Assert.DoesNotContain(reply.Actions, action => action.Id == "all-rooms");
        Assert.Contains("Có 2 phòng khớp", reply.AssistantMessage.Content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Phòng 2 có máy lạnh không?")]
    [InlineData("Phòng thứ hai cọc bao nhiêu?")]
    [InlineData("Phòng nào rẻ nhất?")]
    [InlineData("So sánh 2 phòng đầu giúp mình")]
    public async Task Question_about_listed_rooms_answers_from_retrieved_facts(string message)
    {
        var ai = new FactsClient();
        var reply = await CreateService(new(), ai, new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, message, PreviousCriteria: ListedCriteria));

        Assert.Contains("Phòng 1: Phòng nguồn 1", ai.Facts, StringComparison.Ordinal);
        Assert.Contains("Cọc 1 tháng", ai.Facts, StringComparison.Ordinal);
        Assert.Equal(0, ai.PlainCalls);
        Assert.Null(reply.SearchUpdate);
    }

    [Theory]
    [InlineData("Phòng 2 người dưới 3 triệu")]
    [InlineData("Phòng nào dưới 2 triệu?")]
    [InlineData("Tìm phòng khác")]
    public async Task New_constraints_search_again_instead_of_answering_from_the_old_list(string message)
    {
        var ai = new FactsClient();
        var reply = await CreateService(new(), ai, new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, message, PreviousCriteria: ListedCriteria));

        Assert.Null(ai.Facts);
        Assert.NotNull(reply.SearchUpdate);
    }

    [Theory]
    [InlineData("Mình tên là Minh và đang học ở UIT, bạn ghi nhớ nhé")]
    [InlineData("Giá xăng hôm nay tăng vì sao?")]
    [InlineData("Ai là người phát minh ra điện thoại?")]
    public async Task One_shared_word_does_not_start_a_room_search(string message)
    {
        var ai = new FactsClient();
        var reply = await CreateService(new(), ai, new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, message));

        Assert.Null(reply.SearchUpdate);
        Assert.Equal(1, ai.PlainCalls);
    }

    [Theory]
    [InlineData("Tìm trọ dưới 3 triệu")]
    [InlineData("Gần UIT có gì giá ổn không?")]
    public async Task Housing_requests_still_start_a_search(string message)
    {
        var reply = await CreateService(new(), new FactsClient(), new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, message));

        Assert.NotNull(reply.SearchUpdate);
    }

    [Fact]
    public async Task One_word_can_refine_an_active_search()
    {
        var reply = await CreateService(new(), new FactsClient(), new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, "Thêm máy lạnh", PreviousCriteria: ListedCriteria));

        Assert.Contains("AIR_CONDITIONER", reply.SearchUpdate!.Criteria.RequiredAmenities);
    }

    [Fact]
    public async Task First_message_without_a_list_is_not_a_follow_up()
    {
        var ai = new FactsClient();
        var reply = await CreateService(new(), ai, new GroundedSearchStub(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, "Phòng nào rẻ nhất?"));

        Assert.Null(ai.Facts);
        Assert.NotNull(reply.SearchUpdate);
    }

    [Fact]
    public async Task Search_keeps_working_with_explicit_criteria_when_the_parser_is_down()
    {
        var reply = await CreateService(new(), new SuccessfulClient(), new ParserDownSearch(), sourceListings: new StubSourceListings())
            .SendMessageAsync(new(null, "Tìm phòng dưới 3 triệu ở Thủ Đức"));

        Assert.Equal(3_000_000, reply.SearchUpdate!.Criteria.PriceMax);
        Assert.Equal("Thủ Đức", reply.SearchUpdate.Criteria.Location);
        Assert.Contains("AI đang bận", reply.AssistantMessage.Content, StringComparison.Ordinal);
        Assert.NotNull(reply.SourceRooms);
    }

    [Fact]
    public void Draft_reports_exactly_what_is_missing()
    {
        var details = new AgentPostDetails("Ghế gỗ", "Ghế còn tốt", 0, "Đã dùng", "Nội thất", "", 0, 0);
        var missing = AgentPostBusinessRules.Missing(details, UtcNow, 0, 1);

        Assert.Contains(missing, item => item.Contains("giá", StringComparison.Ordinal));
        Assert.Contains(missing, item => item.Contains("địa chỉ", StringComparison.Ordinal));
        Assert.Contains(missing, item => item.Contains("ảnh", StringComparison.Ordinal));
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
        public Task<ChatbotFoodResponseDto> SearchAsync(string message, ChatbotFoodContextDto? origin, ChatbotFoodCriteriaDto? previous = null, CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatbotFoodResponseDto("empty", "Chưa có món đang bán.", []));
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class UnavailableChatbotAiClient : IChatbotAiClient
    {
        public Task<string> GenerateMediaReplyAsync(IReadOnlyCollection<ChatbotMessageDto> messages, string text, IReadOnlyCollection<ChatbotMediaContent> attachments, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<string> TranscribeAsync(ChatbotMediaContent audio, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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
