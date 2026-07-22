using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IRepositories.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.IServices.AI;
using Homeji.Application.Mappers.Chatbot;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.Chatbot;

public sealed class ChatbotService : IChatbotService
{
    private const int MaxUserMessageLength = 1_000;
    private const int ConversationListLimit = 30;

    private readonly UserContext _userContext;
    private readonly IChatConversationRepository _conversations;
    private readonly IChatbotAiClient _aiClient;
    private readonly IAiSearchService _aiSearch;
    private readonly ChatbotOptions _options;
    private readonly TimeProvider _timeProvider;

    public ChatbotService(
        UserContext userContext,
        IChatConversationRepository conversations,
        IChatbotAiClient aiClient,
        IAiSearchService aiSearch,
        IOptions<ChatbotOptions> options,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _conversations = conversations;
        _aiClient = aiClient;
        _aiSearch = aiSearch;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public Task<ChatbotPopupConfigDto> GetPopupConfigAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ChatbotPopupConfigDto(
            _options.Enabled,
            NormalizeText(_options.Title, "Homeji Assistant"),
            NormalizeText(_options.Greeting, "Xin chào, mình có thể hỗ trợ gì cho bạn?"),
            _options.SuggestedPrompts
                .Where(prompt => !string.IsNullOrWhiteSpace(prompt))
                .Select(prompt => prompt.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(8)
                .ToArray()));
    }

    public async Task<IReadOnlyList<ChatbotConversationDto>> GetMyConversationsAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversations = await _conversations.GetByUserIdAsync(userId, ConversationListLimit, cancellationToken);

        return conversations
            .Select(ChatbotMapper.ToConversationDto)
            .ToArray();
    }

    public async Task<IReadOnlyList<ChatbotMessageDto>> GetMessagesAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversation = await GetOwnedConversationAsync(conversationId, userId, cancellationToken);

        return conversation.Messages
            .OrderBy(message => message.CreatedAt)
            .Select(ChatbotMapper.ToMessageDto)
            .ToArray();
    }

    public async Task<ChatbotReplyDto> SendMessageAsync(
        SendChatbotMessageDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            throw new ForbiddenAccessException("Chatbot is disabled.");
        }

        var message = ValidateMessage(request.Message);
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        var userId = profile.Id;
        var now = _timeProvider.GetUtcNow();

        var conversation = request.ConversationId.HasValue
            ? await GetOwnedConversationAsync(request.ConversationId.Value, userId, cancellationToken)
            : ChatConversation.Create(userId, BuildTitle(message), now);

        if (!request.ConversationId.HasValue)
        {
            await _conversations.AddAsync(conversation, cancellationToken);
        }

        var userMessage = conversation.AddUserMessage(message, now);
        var history = conversation.Messages
            .OrderBy(messageItem => messageItem.CreatedAt)
            .TakeLast(Math.Clamp(_options.MaxHistoryMessages, 2, 30))
            .Select(ChatbotMapper.ToMessageDto)
            .ToArray();

        var assistantReply = await _aiClient.GenerateReplyAsync(history, message, cancellationToken);
        var assistantMessage = conversation.AddAssistantMessage(assistantReply, _timeProvider.GetUtcNow());

        var searchUpdate = await BuildSearchUpdateAsync(conversation, cancellationToken);
        var actions = ChatbotNavigationCatalog.FindActions(message, profile.Role);

        await _conversations.SaveChangesAsync(cancellationToken);

        return new ChatbotReplyDto(
            conversation.Id,
            ChatbotMapper.ToMessageDto(userMessage),
            ChatbotMapper.ToMessageDto(assistantMessage),
            searchUpdate,
            actions);
    }

    private async Task<AiHighlightResponseDto?> BuildSearchUpdateAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        var userMessages = conversation.Messages
            .Where(message => message.Sender == Homeji.Domain.Enums.ChatMessageSender.User)
            .OrderBy(message => message.CreatedAt)
            .TakeLast(Math.Clamp(_options.MaxHistoryMessages, 2, 30))
            .Select(message => message.Content)
            .ToArray();
        var contextualQuery = string.Join(Environment.NewLine, userMessages);
        if (!LooksLikeRentalSearch(contextualQuery))
        {
            return null;
        }

        try
        {
            return await _aiSearch.HighlightRentalPostsAsync(
                new AiHighlightRequestDto(contextualQuery, _options.SearchResultLimit),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // The conversational answer remains usable when the optional map update fails.
            return null;
        }
    }

    private static bool LooksLikeRentalSearch(string text)
    {
        string[] searchTerms =
        [
            "phòng", "trọ", "thuê", "ở ghép", "ngân sách", "giá", "khu vực",
            "gần", "diện tích", "wifi", "bãi xe", "giờ giấc", "toilet", "wc",
        ];

        return searchTerms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<ChatConversation> GetOwnedConversationAsync(
        Guid conversationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var conversation = await _conversations.GetByIdWithMessagesAsync(conversationId, cancellationToken)
            ?? throw new NotFoundException(nameof(ChatConversation), conversationId);

        UserContext.EnsureOwner(userId, conversation.UserId);
        return conversation;
    }

    private static string ValidateMessage(string? message)
    {
        var normalized = message?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["message"] = ["Message is required."],
            });
        }

        if (normalized.Length > MaxUserMessageLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["message"] = [$"Message must not exceed {MaxUserMessageLength} characters."],
            });
        }

        return normalized;
    }

    private static string BuildTitle(string message)
    {
        return message.Length <= ChatConversation.MaxTitleLength
            ? message
            : message[..ChatConversation.MaxTitleLength];
    }

    private static string NormalizeText(string? value, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }
}
