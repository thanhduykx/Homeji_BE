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
using Homeji.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Homeji.Application.UnitTests.Chatbot;

public sealed class ChatbotServiceTests
{
    private static readonly Guid UserId = Guid.Parse("8e996f4c-ec40-4b5e-b66b-9f33c4f29b63");
    private static readonly DateTimeOffset UtcNow = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

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
        bool historyStorageEnabled = true)
    {
        return new ChatbotService(
            new UserContext(new StubCurrentUser(UserId), new StubUserProfileRepository()),
            conversations,
            aiClient,
            search ?? new StubAiSearchService(),
            Options.Create(new ChatbotOptions { HistoryStorageEnabled = historyStorageEnabled }),
            new StubTimeProvider());
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
