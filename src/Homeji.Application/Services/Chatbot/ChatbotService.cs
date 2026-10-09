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
using System.Text.Json;
using Homeji.Application.Services.AI;

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
                .ToArray()) { HistoryStorageEnabled = _options.HistoryStorageEnabled });
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

    public async Task DeleteConversationAsync(Guid conversationId, CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var conversation = await GetOwnedConversationAsync(conversationId, userId, cancellationToken);
        _conversations.Remove(conversation);
        await _conversations.SaveChangesAsync(cancellationToken);
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
        if (request.SaveHistory && !_options.HistoryStorageEnabled)
            throw new ForbiddenAccessException("Lưu lịch sử đang tắt. Bạn vẫn có thể dùng chatbot trong phiên hiện tại.");
        if (!request.SaveHistory && request.ConversationId.HasValue)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["conversationId"] = ["Chỉ gửi mã hội thoại khi đã đồng ý lưu lịch sử."] });
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        var userId = profile.Id;
        var now = _timeProvider.GetUtcNow();

        var conversation = request.ConversationId.HasValue
            ? await GetOwnedConversationAsync(request.ConversationId.Value, userId, cancellationToken)
            : ChatConversation.Create(userId, BuildTitle(message), now);

        if (request.SaveHistory && !request.ConversationId.HasValue)
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
        var searchUpdate = await BuildSearchUpdateAsync(conversation, message, request.PreviousCriteria, cancellationToken);
        var rentalQuery = RentalSearchIntent.IsRentalQuery(message);
        var assistantReply = searchUpdate is not null
            ? BuildGroundedReply(searchUpdate)
            : rentalQuery
                ? "Mình chưa truy vấn được tin phòng lúc này. Bạn có thể dùng bộ lọc thông thường hoặc thử lại. Mình không có dữ liệu để giới thiệu một phòng cụ thể."
                : await GenerateReplyAsync(history, message, actions, cancellationToken);
        var assistantMessage = conversation.AddAssistantMessage(assistantReply, _timeProvider.GetUtcNow());

        if (request.SaveHistory) await _conversations.SaveChangesAsync(cancellationToken);

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
        var knownReply = ChatbotSupportKnowledge.FindReply(message);
        if (knownReply is not null) return knownReply;

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
        string latestMessage,
        AiParsedSearchCriteriaDto? sessionCriteria,
        CancellationToken cancellationToken)
    {
        if (!RentalSearchIntent.IsRentalQuery(latestMessage))
        {
            return null;
        }

        try
        {
            AiParsedSearchCriteriaDto? previous = sessionCriteria;
            if (conversation.SearchCriteriaJson is not null)
                previous = JsonSerializer.Deserialize<AiParsedSearchCriteriaDto>(conversation.SearchCriteriaJson);
            var result = await _aiSearch.HighlightRentalPostsAsync(
                new AiHighlightRequestDto(latestMessage, _options.SearchResultLimit, PreviousCriteria: previous), cancellationToken);
            conversation.UpdateSearchCriteria(JsonSerializer.Serialize(result.Criteria));
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error) when (error is ExternalDependencyException or ExternalServiceUnavailableException or HttpRequestException or JsonException || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // The conversational answer remains usable when the optional map update fails.
            return null;
        }
    }

    private static string BuildGroundedReply(AiHighlightResponseDto result)
    {
        if (result.Criteria.Unknown.Contains("feeUnits", StringComparer.Ordinal))
            return "Bạn đang giới hạn tổng chi phí cả phí. Tin hiện chưa xác nhận đầy đủ đơn vị điện/nước và các khoản thu, nên mình chưa thể bảo đảm phòng nằm trong ngân sách tổng. Nhập và xác nhận kịch bản chi phí của từng tin bên dưới, hoặc đổi sang ‘chỉ tiền thuê’.";
        if (result.Criteria.Unknown.Contains("destination", StringComparer.Ordinal))
            return "Bạn muốn tới điểm trường hay nơi làm việc cụ thể nào? Nếu là FPT, hãy ghi rõ cơ sở, ví dụ ‘FPT Khu Công nghệ cao’. Mình chưa tính được đường đi và sẽ không gọi khoảng cách đường thẳng là thời gian di chuyển.";
        if (result.Criteria.Unknown.Contains("commute", StringComparer.Ordinal))
            return "Đã ghi nhận điểm đến. Chọn địa chỉ điểm đến và phương tiện trong công cụ bên dưới rồi nhấn tính tuyến và xác nhận kết quả. Mình chưa coi phòng nào là đáp ứng yêu cầu gần trường. Bạn có thể bỏ điểm đến để tìm theo các điều kiện còn lại trước.";
        if (result.Criteria.Unknown.Contains("occupants", StringComparer.Ordinal))
            return "Bạn cần phòng cho bao nhiêu người? Nhập số nguyên từ 1 đến 20 để mình kiểm tra số chỗ còn lại theo tin đăng.";
        if (result.Criteria.Unknown.Contains("area", StringComparer.Ordinal))
            return "Bạn muốn diện tích bao nhiêu m²? Hãy nhập một mức hoặc khoảng diện tích hợp lệ, ví dụ ‘từ 20 đến 30 m²’.";
        if (result.Criteria.Unknown.Count > 0)
            return "Mình cần bạn làm rõ mức ngân sách hoặc khoảng giá. Với ‘rẻ hơn’, hãy cho mình trần giá mới, ví dụ ‘dưới 3 triệu’, để không tự thay đổi điều kiện của bạn.";
        if (result.Posts.Count == 0)
            return "Chưa có tin công khai phù hợp các điều kiện này trong phạm vi Homeji. Bạn muốn nới điều kiện nào? Mình sẽ giữ ngân sách và yêu cầu hiện tại cho tới khi bạn sửa.";
        return $"Tìm được {result.Posts.Count} tin công khai đáp ứng bộ lọc đã hiểu. Xem tiêu chí và các tin bên dưới, rồi xác nhận để áp dụng lên bản đồ. Giá và tiện ích lấy từ dữ liệu chủ tin, chưa đồng nghĩa Homeji đã xác minh hay phòng vẫn còn trống. Điểm phù hợp được tách khỏi ưu tiên thương mại.";
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
