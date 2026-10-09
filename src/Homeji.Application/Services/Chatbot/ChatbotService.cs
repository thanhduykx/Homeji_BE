using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IRepositories.Chatbot;
using Homeji.Application.IServices.Chatbot;
using Homeji.Application.IServices.AI;
using Homeji.Application.Mappers.Chatbot;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.AI;
using Homeji.Domain.Entities;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
            NormalizeText(_options.Title, "Homeji"),
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
            throw new ForbiddenAccessException("Chatbot hiện đang tắt.");
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

        var actions = ChatbotNavigationCatalog.FindActions(message, profile.Role);
        var searchUpdate = await BuildSearchUpdateAsync(conversation, cancellationToken);
        var assistantReply = searchUpdate is null
            ? await GenerateReplyAsync(history, message, actions, cancellationToken)
            : BuildGroundedReply(searchUpdate);
        var assistantMessage = conversation.AddAssistantMessage(assistantReply, _timeProvider.GetUtcNow());

        await _conversations.SaveChangesAsync(cancellationToken);

        return new ChatbotReplyDto(
            conversation.Id,
            ChatbotMapper.ToMessageDto(userMessage),
            ChatbotMapper.ToMessageDto(assistantMessage),
            searchUpdate,
            actions);
    }

    private async Task<string> GenerateReplyAsync(
        IReadOnlyCollection<ChatbotMessageDto> history,
        string message,
        IReadOnlyCollection<ChatbotNavigationActionDto> actions,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _aiClient.GenerateReplyAsync(history, message, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ExternalServiceUnavailableException)
        {
            return ChatbotFallbackReply.Create(actions);
        }
        catch (HttpRequestException)
        {
            return ChatbotFallbackReply.Create(actions);
        }
        catch (TaskCanceledException)
        {
            return ChatbotFallbackReply.Create(actions);
        }
    }

    private async Task<AiHighlightResponseDto?> BuildSearchUpdateAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        var userMessages = conversation.Messages
            .Where(item => item.Sender == Homeji.Domain.Enums.ChatMessageSender.User)
            .OrderBy(item => item.CreatedAt).ToArray();
        if (!userMessages.Any(item => LooksLikeRentalSearch(item.Content))) return null;
        var latest = userMessages.Last().Content;
        if (ChatbotNavigationCatalog.FindActions(latest, Homeji.Domain.Enums.UserRole.Renter).Count > 0
            && !LooksLikeRentalSearch(latest)) return null;
        // Store a bounded schema owned by this conversation, never concatenate prior prompts.
        var intent = RentalSearchIntent.Empty();
        if (conversation.SearchIntentJson is not null)
        {
            try { intent = JsonSerializer.Deserialize<AiParsedSearchCriteriaDto>(conversation.SearchIntentJson) ?? intent; }
            catch (JsonException) { intent = RentalSearchIntent.Empty(); }
        }
        else
            foreach (var item in userMessages.Take(userMessages.Length - 1)) intent = RentalSearchIntent.Apply(item.Content, intent);
        try
        {
            var parsed = await _aiSearch.ParseSearchAsync(new AiParseSearchRequestDto(latest), cancellationToken);
            intent = RentalSearchIntent.Apply(latest, RentalSearchIntent.Merge(intent, parsed));
            var result = await _aiSearch.HighlightRentalPostsAsync(
                new AiHighlightRequestDto(null, Math.Clamp(_options.SearchResultLimit, 1, 5), intent), cancellationToken);
            result = result with { CompareRequested = RentalSearchIntent.Normalize(latest).Contains("so sanh", StringComparison.Ordinal) };
            conversation.RememberSearchIntent(JsonSerializer.Serialize(result.Criteria));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ExternalDependencyException) { return new AiHighlightResponseDto(intent, [], "Phù hợp theo tiêu chí", null, null, null) { Clarifications = ["Chưa thể truy vấn tin. Vui lòng thử lại hoặc dùng bộ lọc thông thường."] }; }
    }

    private static string BuildGroundedReply(AiHighlightResponseDto result)
    {
        var lines = new List<string> { "Homeji hiểu tiêu chí của bạn; bạn có thể sửa hoặc bỏ từng tiêu chí bên dưới." };
        lines.AddRange(result.Clarifications);
        if (result.Posts.Count > 0)
            lines.Add($"Tìm thấy {result.Posts.Count} tin phù hợp. Giá và tiện ích bên dưới lấy từ tin chủ phòng, không phải xác minh của Homeji.");
        else if (result.NeedsConfirmation.Count > 0)
            lines.Add("Các tin bên dưới cần xác nhận phí hoặc đường đi; chưa thể khẳng định đáp ứng toàn bộ yêu cầu.");
        else
            lines.Add("Chưa có tin đáp ứng các điều kiện. Bạn có muốn bỏ một tiện ích bắt buộc hoặc chọn khu vực khác trong phạm vi Homeji? Ngân sách vẫn được giữ nguyên.");
        return string.Join(Environment.NewLine, lines);
    }

    private static bool LooksLikeRentalSearch(string text)
    {
        string[] searchTerms =
        [
            "phòng", "trọ", "thuê", "ở ghép", "ngân sách", "giá", "khu vực",
            "gần", "diện tích", "wifi", "bãi xe", "giờ giấc", "toilet", "wc",
        ];

        var normalized = RentalSearchIntent.Normalize(text);
        var intent = RentalSearchIntent.Apply(text);
        return searchTerms.Any(term => normalized.Contains(RentalSearchIntent.Normalize(term), StringComparison.Ordinal))
            || intent.PriceMin.HasValue || intent.PriceMax.HasValue || intent.Occupants.HasValue
            || intent.AreaMin.HasValue || intent.AreaMax.HasValue || intent.Destination is not null
            || intent.Location is not null || intent.RequiredAmenities.Count > 0
            || intent.Criteria.Count > 0 || intent.ExcludedAmenities.Count > 0;
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
                ["message"] = ["Tin nhắn là bắt buộc."],
            });
        }

        if (normalized.Length > MaxUserMessageLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["message"] = [$"Tin nhắn không được vượt quá {MaxUserMessageLength} ký tự."],
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
