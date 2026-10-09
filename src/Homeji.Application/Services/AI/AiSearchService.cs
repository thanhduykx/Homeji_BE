using Homeji.Application.Common;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.AI;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.AI;

public sealed class AiSearchService : IAiSearchService
{
    private const string HighlightTag = "Phù hợp theo tiêu chí";
    private readonly IAiSearchTextParser _parser;
    private readonly IRentalPostRepository _posts;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly AiSearchOptions _options;
    private readonly TimeProvider _timeProvider;

    public AiSearchService(IAiSearchTextParser parser, IRentalPostRepository posts,
        IUserSubscriptionRepository subscriptions, IRentalReviewRepository reviews,
        IOptions<AiSearchOptions> options, TimeProvider timeProvider)
    {
        _parser = parser;
        _posts = posts;
        _subscriptions = subscriptions;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<AiParsedSearchCriteriaDto> ParseSearchAsync(AiParseSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateText(request.Text);
        AiParsedSearchCriteriaDto parsed;
        try { parsed = await _parser.ParseAsync(text, cancellationToken); }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is ExternalDependencyException or ExternalServiceUnavailableException
            or HttpRequestException or System.Text.Json.JsonException or TaskCanceledException
            or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        { parsed = RentalSearchIntent.Empty(); }
        return ValidateIntent(RentalSearchIntent.Apply(text, parsed));
    }

    public async Task<AiHighlightResponseDto> HighlightRentalPostsAsync(AiHighlightRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var parsed = request.Intent is null
            ? await ParseSearchAsync(new AiParseSearchRequestDto(request.Text), cancellationToken)
            : ValidateIntent(string.IsNullOrWhiteSpace(request.Text) ? request.Intent : RentalSearchIntent.Apply(ValidateText(request.Text), request.Intent));
        var limit = Math.Clamp(request.MaxResults, 1, Math.Clamp(_options.MaxHighlightedPosts, 1, 10));
        var search = new RentalPostSearchDto(null, parsed.PriceMin, parsed.PriceMax,
            parsed.AreaMin, parsed.AreaMax, HomejiServiceArea.MinLatitude, HomejiServiceArea.MaxLatitude,
            HomejiServiceArea.MinLongitude, HomejiServiceArea.MaxLongitude, parsed.RequiredAmenities,
            1, 100, MinAvailableSlots: parsed.Occupants);
        var posts = await _posts.SearchActiveAsync(search, cancellationToken);
        // Recheck constraints even when repository implementations change; no model can relax them.
        var now = _timeProvider.GetUtcNow();
        var candidates = posts.Where(post => Fits(post, parsed, DateOnly.FromDateTime(now.UtcDateTime))).ToArray();
        var premium = await _subscriptions.GetActivePremiumByUserIdsAsync(candidates.Select(post => post.OwnerId).ToArray(), now, cancellationToken);
        var ranked = candidates.Select(post => EvaluateFit(post, parsed, premium.ContainsKey(post.OwnerId)))
            .OrderByDescending(item => item.Score).ThenBy(item => item.Post.Price).ThenBy(item => item.Post.Id)
            .Take(limit).ToArray();
        // Legacy fee fields lack confirmed units. Never advertise them as satisfying an all-in ceiling.
        var needsConfirmation = parsed.BudgetKind == "total" || parsed.MaxCommuteMinutes.HasValue || parsed.Destination is not null;
        var confirmed = needsConfirmation ? [] : ranked;
        var focus = confirmed.FirstOrDefault()?.Post;
        return new AiHighlightResponseDto(parsed, confirmed, HighlightTag, focus?.Address, focus?.Latitude, focus?.Longitude)
        {
            NeedsConfirmation = needsConfirmation ? ranked : [],
            Clarifications = parsed.Unknown,
        };
    }

    public static bool Fits(RentalPost post, AiParsedSearchCriteriaDto intent, DateOnly today)
    {
        if (post.Status != RentalPostStatus.Active || post.IsSynthetic || !HomejiServiceArea.Contains(post.Latitude, post.Longitude)
            || post.Type == RentalPostType.RoomTransfer && post.OriginalLeaseEndsOn <= today
            || intent.PriceMin.HasValue && post.Price < intent.PriceMin || intent.PriceMax.HasValue && post.Price > intent.PriceMax
            || intent.AreaMin.HasValue && post.Area < intent.AreaMin || intent.AreaMax.HasValue && post.Area > intent.AreaMax
            || intent.Occupants.HasValue && post.AvailableSlots < intent.Occupants
            || intent.ExcludeShared && post.Type == RentalPostType.RoommateShare) return false;
        var codes = post.Amenities.Select(amenity => amenity.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!intent.RequiredAmenities.All(codes.Contains) || intent.ExcludedAmenities.Any(codes.Contains)) return false;
        if (intent.Location is not null && !RentalSearchIntent.Normalize(post.Address).Contains(RentalSearchIntent.Normalize(intent.Location), StringComparison.Ordinal)) return false;
        if (intent.Keyword is not null && !RentalSearchIntent.Normalize(post.Title + " " + post.Description + " " + post.Address).Contains(RentalSearchIntent.Normalize(intent.Keyword), StringComparison.Ordinal)) return false;
        return true;
    }

    public static AiHighlightedRentalPostDto EvaluateFit(RentalPost post, AiParsedSearchCriteriaDto intent, bool premium)
    {
        var evidence = new List<AiEvidenceDto>();
        void Add(string field, string text) => evidence.Add(new(post.Id, "ownerListing", field, text, post.UpdatedAt));
        Add("price", intent.BudgetKind == "total" ? "Tiền thuê nằm dưới trần; tổng phí chưa được xác nhận." : "Giá thuê theo tin đăng.");
        if (intent.PriceMax.HasValue && intent.BudgetKind == "rent") Add("price", "Giá thuê nằm trong ngân sách tối đa.");
        if (intent.Location is not null) Add("address", "Địa chỉ tin đăng khớp khu vực yêu cầu.");
        if (intent.Occupants.HasValue) Add("availableSlots", "Số chỗ trống theo tin đáp ứng số người.");
        foreach (var code in intent.RequiredAmenities.Concat(intent.Criteria).Distinct(StringComparer.Ordinal))
            if (post.Amenities.Any(amenity => amenity.Code.Equals(code, StringComparison.OrdinalIgnoreCase)))
                Add("amenities." + code, "Có " + code + " theo tiện ích chủ tin khai báo.");
        var score = 5m + (intent.PriceMax.HasValue ? 25 : 0) + (intent.Location is not null ? 30 : 0)
            + evidence.Count(item => item.Field.StartsWith("amenities.", StringComparison.Ordinal)) * 12;
        var summary = RentalPostMapper.ToSummaryDto(post, premium, premium ? 100 : 0, HighlightTag);
        return new AiHighlightedRentalPostDto(summary, score, evidence.Select(item => item.Text).ToArray(), HighlightTag) { Evidence = evidence };
    }

    private static string ValidateText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 1000)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["text"] = ["Nhập nội dung từ 1 đến 1.000 ký tự."] });
        return text.Trim();
    }

    private static AiParsedSearchCriteriaDto ValidateIntent(AiParsedSearchCriteriaDto intent)
    {
        if (intent.PriceMin < 0 || intent.PriceMax <= 0 || intent.AreaMin < 0 || intent.AreaMax <= 0
            || intent.PriceMin > intent.PriceMax || intent.AreaMin > intent.AreaMax
            || intent.Occupants is < 1 or > 50 || intent.MaxCommuteMinutes is < 1 or > 240
            || intent.BudgetKind is not ("rent" or "total"))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["intent"] = ["Tiêu chí tìm kiếm không hợp lệ."] });
        string[] Codes(IEnumerable<string> values) => values.Where(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 60)
            .Select(RentalSearchIntent.AmenityCode).Distinct(StringComparer.Ordinal).Take(20).ToArray();
        var required = Codes(intent.RequiredAmenities);
        var excluded = Codes(intent.ExcludedAmenities);
        if (required.Intersect(excluded, StringComparer.Ordinal).Any())
            throw new RequestValidationException(new Dictionary<string, string[]> { ["intent"] = ["Một tiện ích không thể vừa bắt buộc vừa loại trừ."] });
        return intent with
        {
            RequiredAmenities = required, ExcludedAmenities = excluded, Criteria = Codes(intent.Criteria),
            Unknown = intent.Unknown.Where(value => !string.IsNullOrWhiteSpace(value)).Take(6).Select(value => value.Length <= 180 ? value : value[..180]).ToArray(),
        };
    }
}
