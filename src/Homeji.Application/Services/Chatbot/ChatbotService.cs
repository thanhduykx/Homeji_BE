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
    private readonly IRentalDecisionService _rentalDecision;
    private readonly IChatbotFoodService _food;
    private readonly ChatbotOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly IChatbotPostingAgent? _postingAgent;
    private readonly Homeji.Application.IServices.Roommates.IRoommateDirectoryService? _roommates;
    private readonly IChatbotNearbyService? _nearby;

    public ChatbotService(
        UserContext userContext,
        IChatConversationRepository conversations,
        IChatbotAiClient aiClient,
        IAiSearchService aiSearch,
        IRentalDecisionService rentalDecision,
        IChatbotFoodService food,
        IOptions<ChatbotOptions> options,
        TimeProvider timeProvider, IChatbotPostingAgent? postingAgent = null, IChatbotNearbyService? nearby = null, Homeji.Application.IServices.Roommates.IRoommateDirectoryService? roommates = null)
    {
        _userContext = userContext;
        _conversations = conversations;
        _aiClient = aiClient;
        _aiSearch = aiSearch;
        _rentalDecision = rentalDecision;
        _food = food;
        _options = options.Value;
        _timeProvider = timeProvider;
        _postingAgent = postingAgent;
        _nearby = nearby;
        _roommates = roommates;
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

        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        request.Location?.Validate(_timeProvider.GetUtcNow());
        var media = ChatbotMediaValidation.Decode(request.Attachments);
        var message = ValidateMessage(string.IsNullOrWhiteSpace(request.Message) && media.Count > 0
            ? "Hãy xem hoặc nghe nội dung đính kèm và hỗ trợ mình." : request.Message);
        var foodRefinement = request.PreviousFoodCriteria is not null && FoodSearchIntent.IsRefinement(message);
        var foodTopic = FoodSearchIntent.IsFoodTopic(message) || foodRefinement;
        var foodQuery = foodRefinement || FoodSearchIntent.IsDiscovery(message);
        var selectionTask = foodTopic ? null : ChatbotRentalSelection.ResolveTask(request.RentalSelection, request.RentalContext, message);
        var rentalTask = foodTopic ? null : ChatbotRentalContext.ResolveTask(request.RentalContext, message);
        if (request.SaveHistory && !_options.HistoryStorageEnabled)
            throw new ForbiddenAccessException("Lưu lịch sử đang tắt. Bạn vẫn có thể dùng chatbot trong phiên hiện tại.");
        if (!request.SaveHistory && request.ConversationId.HasValue)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["conversationId"] = ["Chỉ gửi mã hội thoại khi đã đồng ý lưu lịch sử."] });
        var userId = profile.Id;
        var now = _timeProvider.GetUtcNow();

        var conversation = request.ConversationId.HasValue
            ? await GetOwnedConversationAsync(request.ConversationId.Value, userId, cancellationToken)
            : ChatConversation.Create(userId, BuildTitle(message), now);

        if (request.SaveHistory && !request.ConversationId.HasValue)
        {
            await _conversations.AddAsync(conversation, cancellationToken);
        }

        var storedMessage = media.Count > 0
            ? message + $"\n[Đã gửi {media.Count} tệp; ảnh và âm thanh không được lưu trong lịch sử.]" : message;
        var userMessage = conversation.AddUserMessage(storedMessage, now);
        var history = conversation.Messages
            .OrderBy(messageItem => messageItem.CreatedAt)
            .TakeLast(Math.Clamp(_options.MaxHistoryMessages, 2, 30))
            .Select(ChatbotMapper.ToMessageDto)
            .ToArray();

        if (!request.SaveHistory) history = ChatbotSessionContext.Bound(request.SessionMessages).Concat(history.TakeLast(1)).ToArray();
        history = ChatbotSessionContext.Bound(history).ToArray();
        AgentPostResultDto? agentResult = null;
        IReadOnlyCollection<ChatbotNavigationActionDto> actions = [];
        AiHighlightResponseDto? searchUpdate = null;
        RentalDecisionResponseDto? rentalDecision = null;
        ChatbotFoodResponseDto? foodDiscovery = null;
        ChatbotNearbyDto? nearbyRooms = null;
        var nearbyQuery = ChatbotSessionContext.RequestsNearbyRooms(message);
        IReadOnlyList<Homeji.Application.DTOs.Roommates.RoommateDirectoryCandidateDto>? roommateCandidates = null;
        string assistantReply;
        var postingHistory = request.SaveHistory
            ? ChatbotSessionContext.Bound(conversation.Messages.Where(row => row.Id != userMessage.Id).OrderBy(row => row.CreatedAt).Select(ChatbotMapper.ToMessageDto))
            : ChatbotSessionContext.Bound(request.SessionMessages);
        var postingMessage = !foodQuery && !nearbyQuery
            ? ChatbotPostingContext.Resolve(message, postingHistory, request.AgentDraftId.HasValue) : null;
        // An explicit posting command wins over discovery keyword overlap.
        if (AgentBusinessIntent.PostingKind(message) is not null) postingMessage = message;
        if (_postingAgent is not null && postingMessage is not null)
        {
            var agentMessage = postingMessage + "\nYêu cầu người dùng gần đây (chỉ tham khảo nếu cùng món đồ; yêu cầu mới nhất được ưu tiên): "
                + JsonSerializer.Serialize(history.Where(row => row.Sender == Homeji.Domain.Enums.ChatMessageSender.User)
                    .TakeLast(3).Select(row => row.Content));
            agentResult = await _postingAgent.PrepareAsync(agentMessage, media, request.AgentDraftId, request.Location, cancellationToken);
            assistantReply = agentResult.Message;
            if (agentResult.UpgradeRequired)
            {
                var kind = AgentBusinessIntent.PostingKind(message);
                var target = kind switch { "rental" or "roommate" => profile.Role == Homeji.Domain.Enums.UserRole.Landlord ? "/posts/new" : "/?section=profile",
                    "roomTransfer" => "/posts/new?type=pass", "wanted" => "/?section=wanted", _ => "/?section=marketplace" };
                actions = [new("manual-post", "Mở biểu mẫu phù hợp", "Tự điền thông tin và kiểm tra trước khi đăng", ChatbotNavigationActionKind.Navigate, target)];
            }
        }
        else if (AgentBusinessIntent.IsRoommateSearch(message))
        {
            if (_roommates is not null && profile.Role == Homeji.Domain.Enums.UserRole.Renter)
            {
                var directory = await _roommates.SearchAsync(null, null, 1, 5, cancellationToken);
                roommateCandidates = directory.Items;
                assistantReply = directory.Items.Count > 0
                    ? "Đây là các hồ sơ đang công khai tìm ở ghép trên Homeji. Mức tương đồng (nếu có) chỉ dựa trên thói quen đã khai; bạn cần kiểm tra khu vực, ngân sách và trao đổi trước khi gửi lời mời. Mình chưa gửi lời mời thay bạn."
                    : "Chưa có hồ sơ công khai tìm ở ghép lúc này. Bạn có thể mở mục Ở ghép để cập nhật nhu cầu và bật xuất hiện trong danh sách.";
            }
            else assistantReply = "Mở mục Ở ghép để xem và cập nhật nhu cầu. Tìm hồ sơ bạn ở ghép yêu cầu tài khoản người thuê; chủ trọ có thể đăng tin phòng tìm người ở ghép.";
            actions = [new("roommate", "Tìm bạn ở ghép", "Xem người đang công khai tìm ở ghép", ChatbotNavigationActionKind.OpenSection, "invitations")];
        }
        else if (nearbyQuery && _nearby is not null)
        {
            nearbyRooms = await _nearby.SearchAsync(message, request.Location, cancellationToken);
            assistantReply = nearbyRooms.Summary;
        }
        else if (media.Count > 0)
        {
            // Media is untrusted input, never a source of verified catalog/account data.
            // Provider errors propagate so the sender can retry the actual attachment.
            assistantReply = await _aiClient.GenerateMediaReplyAsync(history, message, media, cancellationToken);
        }
        else if (foodQuery)
        {
            foodDiscovery = await _food.SearchAsync(message, request.FoodContext ?? (request.Location is null ? null : new ChatbotFoodContextDto(request.Location.Latitude, request.Location.Longitude)), request.PreviousFoodCriteria, cancellationToken);
            assistantReply = foodDiscovery.Summary;
            actions = [new ChatbotNavigationActionDto("food-menu", "Mở Chợ đồ ăn", "Xem thực đơn và tự chọn món", ChatbotNavigationActionKind.OpenSection, "marketplace:food")];
        }
        else if (selectionTask is not null)
        {
            rentalDecision = await _rentalDecision.CompareAsync(new RentalDecisionRequestDto(
                request.RentalSelection!.PostIds!, request.RentalSelection.Scenario), cancellationToken);
            assistantReply = ChatbotRentalSelection.BuildReply(selectionTask, rentalDecision);
        }
        else if (rentalTask is not null)
        {
            rentalDecision = await _rentalDecision.CompareAsync(
                new RentalDecisionRequestDto([request.RentalContext!.PostId]), cancellationToken);
            assistantReply = ChatbotRentalContext.BuildReply(rentalTask, rentalDecision);
        }
        else
        {
            actions = ChatbotNavigationCatalog.FindActions(message, profile.Role);
            searchUpdate = await BuildSearchUpdateAsync(conversation, message, request.PreviousCriteria, cancellationToken);
            var rentalQuery = RentalSearchIntent.IsRentalQuery(message);
            assistantReply = searchUpdate is not null
                ? BuildGroundedReply(searchUpdate)
                : rentalQuery
                    ? "Mình chưa truy vấn được tin phòng lúc này. Bạn có thể dùng bộ lọc thông thường hoặc thử lại. Mình không có dữ liệu để giới thiệu một phòng cụ thể."
                    : await GenerateReplyAsync(history, message, actions, cancellationToken);
        }
        if (agentResult is null) assistantReply = ChatbotActionClaims.WithoutUnverifiedPublishing(assistantReply);
        var assistantMessage = conversation.AddAssistantMessage(assistantReply, _timeProvider.GetUtcNow());

        if (request.SaveHistory) await _conversations.SaveChangesAsync(cancellationToken);

        return new ChatbotReplyDto(
            conversation.Id,
            ChatbotMapper.ToMessageDto(userMessage),
            ChatbotMapper.ToMessageDto(assistantMessage),
            searchUpdate,
            actions) { RoommateCandidates = roommateCandidates, RentalDecision = rentalDecision, FoodDiscovery = foodDiscovery, NearbyRooms = nearbyRooms, AgentDraft = agentResult?.Draft, AgentRequested = agentResult is not null && !agentResult.UpgradeRequired, UpgradeRequired = agentResult?.UpgradeRequired == true };
    }

    public async Task<string> TranscribeAsync(ChatbotTranscriptionDto request, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) throw new ForbiddenAccessException("Chatbot hiện đang tắt.");
        await _userContext.GetRequiredProfileAsync(cancellationToken);
        if (request.Audio is null) throw ChatbotMediaValidation.Invalid();
        var media = ChatbotMediaValidation.Decode([request.Audio]);
        if (media[0].MimeType != "audio/wav") throw ChatbotMediaValidation.Invalid();
        return ValidateMessage(await _aiClient.TranscribeAsync(media[0], cancellationToken));
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
