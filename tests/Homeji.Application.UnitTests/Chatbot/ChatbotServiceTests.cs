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
        Assert.Equal(1, conversations.SaveCount);
        Assert.Equal(2, conversations.Conversation!.Messages.Count);
    }

    private static ChatbotService CreateService(
        InMemoryChatConversationRepository conversations,
        IChatbotAiClient aiClient)
    {
        return new ChatbotService(
            new UserContext(new StubCurrentUser(UserId), new StubUserProfileRepository()),
            conversations,
            aiClient,
            new StubAiSearchService(),
            Options.Create(new ChatbotOptions()),
            new StubTimeProvider());
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
