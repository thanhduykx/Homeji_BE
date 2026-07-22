using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IRepositories.Subscriptions;
using Homeji.Application.IServices.AI;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Homeji.Application.Services.AI;

public sealed class AiSearchService : IAiSearchService
{
    private const string AiHighlightTag = "AI bảo thế đó";
    private const int MaxSearchTextLength = 1_000;
    private const int MaxCandidatePosts = 100;

    private static readonly Dictionary<string, string[]> CriterionSynonyms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["parking"] = ["PARKING", "giu xe", "giữ xe", "xe", "parking"],
            ["freeTime"] = ["FREE_TIME", "gio giac", "giờ giấc", "tu do", "tự do", "free time"],
            ["wifi"] = ["WIFI", "internet", "wifi"],
            ["airConditioner"] = ["AIR_CONDITIONER", "may lanh", "máy lạnh", "dieu hoa", "điều hòa"],
            ["privateToilet"] = ["PRIVATE_TOILET", "wc rieng", "wc riêng", "ve sinh rieng", "vệ sinh riêng"],
            ["security"] = ["SECURITY", "an ninh", "bao ve", "bảo vệ"],
            ["quiet"] = ["QUIET", "yen tinh", "yên tĩnh"],
            ["petFriendly"] = ["PET_FRIENDLY", "thu cung", "thú cưng", "pet"],
            ["kitchen"] = ["KITCHEN", "bep", "bếp", "nau an", "nấu ăn"],
        };

    private readonly IAiSearchTextParser _parser;
    private readonly IRentalPostRepository _posts;
    private readonly IUserSubscriptionRepository _subscriptions;
    private readonly IRentalReviewRepository _reviews;
    private readonly AiSearchOptions _options;
    private readonly TimeProvider _timeProvider;

    public AiSearchService(
        IAiSearchTextParser parser,
        IRentalPostRepository posts,
        IUserSubscriptionRepository subscriptions,
        IRentalReviewRepository reviews,
        IOptions<AiSearchOptions> options,
        TimeProvider timeProvider)
    {
        _parser = parser;
        _posts = posts;
        _subscriptions = subscriptions;
        _reviews = reviews;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<AiParsedSearchCriteriaDto> ParseSearchAsync(
        AiParseSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateText(request.Text);
        var parsed = await _parser.ParseAsync(text, cancellationToken);

        return NormalizeParsedCriteria(parsed);
    }

    public async Task<AiHighlightResponseDto> HighlightRentalPostsAsync(
        AiHighlightRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateText(request.Text);
        var parsed = NormalizeParsedCriteria(await _parser.ParseAsync(text, cancellationToken));
        var maxResults = Math.Clamp(
            request.MaxResults <= 0 ? _options.MaxHighlightedPosts : request.MaxResults,
            1,
            Math.Max(1, _options.MaxHighlightedPosts));

        var search = new RentalPostSearchDto(
            BuildKeyword(parsed),
            parsed.PriceMin,
            parsed.PriceMax,
            parsed.AreaMin,
            parsed.AreaMax,
            null,
            null,
            null,
            null,
            [],
            1,
            MaxCandidatePosts);

        var posts = await _posts.SearchActiveAsync(search, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var premiumByUserId = await _subscriptions.GetActivePremiumByUserIdsAsync(
            posts.Select(post => post.OwnerId).ToArray(),
            now,
            cancellationToken);
        var reviewItems = await _reviews.GetByPostIdsAsync(
            posts.Select(post => post.Id).ToArray(),
            cancellationToken);
        var reviewsByPostId = reviewItems
            .GroupBy(review => review.RentalPostId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RentalReview>)group.ToArray());

        var rankedPosts = posts
            .Select(post =>
            {
                var isPremium = premiumByUserId.ContainsKey(post.OwnerId);
                var postReviews = reviewsByPostId.GetValueOrDefault(post.Id) ?? [];
                var score = CalculateAiScore(post, postReviews, parsed, isPremium, now, out var reasons);
                var summary = RentalPostMapper.ToSummaryDto(
                    post,
                    isPremium,
                    CalculateBoostScore(post, isPremium, now),
                    AiHighlightTag);

                return new AiHighlightedRentalPostDto(summary, score, reasons, AiHighlightTag);
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Post.IsOwnerPremium)
            .ThenByDescending(item => item.Post.BoostScore)
            .Take(maxResults)
            .ToArray();

        var mapFocus = rankedPosts.FirstOrDefault()?.Post;

        return new AiHighlightResponseDto(
            parsed,
            rankedPosts,
            AiHighlightTag,
            parsed.Location ?? mapFocus?.Address,
            mapFocus?.Latitude,
            mapFocus?.Longitude);
    }

    private static string ValidateText(string? text)
    {
        var normalized = text?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["text"] = ["Nội dung tìm kiếm là bắt buộc."],
            });
        }

        if (normalized.Length > MaxSearchTextLength)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["text"] = [$"Nội dung tìm kiếm không được vượt quá {MaxSearchTextLength} ký tự."],
            });
        }

        return normalized;
    }

    private static AiParsedSearchCriteriaDto NormalizeParsedCriteria(AiParsedSearchCriteriaDto criteria)
    {
        var normalizedCriteria = criteria.Criteria
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();

        return criteria with
        {
            Location = NormalizeOptional(criteria.Location),
            Keyword = NormalizeOptional(criteria.Keyword),
            PriceMin = NormalizePositive(criteria.PriceMin),
            PriceMax = NormalizePositive(criteria.PriceMax),
            AreaMin = NormalizePositive(criteria.AreaMin),
            AreaMax = NormalizePositive(criteria.AreaMax),
            Criteria = normalizedCriteria,
        };
    }

    private static string? BuildKeyword(AiParsedSearchCriteriaDto criteria)
    {
        return NormalizeOptional(criteria.Location) ?? NormalizeOptional(criteria.Keyword);
    }

    private static decimal CalculateAiScore(
        RentalPost post,
        IReadOnlyCollection<RentalReview> reviews,
        AiParsedSearchCriteriaDto criteria,
        bool isPremium,
        DateTimeOffset now,
        out IReadOnlyCollection<string> reasons)
    {
        var score = 0m;
        var resultReasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(criteria.Location)
            && (ContainsNormalized(post.Address, criteria.Location)
                || ContainsNormalized(post.Title, criteria.Location)))
        {
            score += 30;
            resultReasons.Add("Phù hợp khu vực người dùng yêu cầu.");
        }

        if (criteria.PriceMax.HasValue && post.Price <= criteria.PriceMax.Value)
        {
            score += 25;
            resultReasons.Add("Giá nằm trong ngân sách tối đa.");
        }
        else if (!criteria.PriceMax.HasValue)
        {
            score += 5;
        }

        if (criteria.PriceMin.HasValue && post.Price >= criteria.PriceMin.Value)
        {
            score += 5;
        }

        foreach (var criterion in criteria.Criteria)
        {
            if (MatchesCriterion(post, reviews, criterion, out var matchedFromReview))
            {
                score += 12;
                resultReasons.Add(matchedFromReview
                    ? $"Đánh giá cộng đồng xác nhận tiêu chí: {criterion}."
                    : $"Khớp tiêu chí: {criterion}.");
            }
        }

        var recencyDays = Math.Max(0, (now - post.UpdatedAt).TotalDays);
        score += Math.Max(0, 10 - (decimal)recencyDays);
        score += Math.Min(15, post.SaveCount * 3);
        score += Math.Min(10, post.ViewCount / 20m);

        if (isPremium)
        {
            score += 8;
            resultReasons.Add("Chủ bài có Premium nên được ưu tiên hiển thị.");
        }

        if (resultReasons.Count == 0)
        {
            resultReasons.Add("Được chọn dựa trên mức độ liên quan tổng hợp.");
        }

        reasons = resultReasons;
        return Math.Round(score, 2);
    }

    private static decimal CalculateBoostScore(RentalPost post, bool isPremium, DateTimeOffset now)
    {
        var recencyDays = Math.Max(0, (now - post.UpdatedAt).TotalDays);
        var recencyScore = Math.Max(0, 30 - (decimal)recencyDays);
        var engagementScore = (post.SaveCount * 5) + Math.Min(post.ViewCount, 500) / 10m;
        var premiumScore = isPremium ? 100 : 0;

        return Math.Round(premiumScore + engagementScore + recencyScore, 2);
    }

    private static bool MatchesCriterion(
        RentalPost post,
        IReadOnlyCollection<RentalReview> reviews,
        string criterion,
        out bool matchedFromReview)
    {
        var terms = CriterionSynonyms.TryGetValue(criterion, out var synonyms)
            ? synonyms
            : [criterion];

        var matchedFromPost = terms.Any(term =>
            post.Amenities.Any(amenity => amenity.Code.Equals(term, StringComparison.OrdinalIgnoreCase))
            || ContainsNormalized(post.Title, term)
            || ContainsNormalized(post.Description, term)
            || ContainsNormalized(post.Address, term));
        if (matchedFromPost)
        {
            matchedFromReview = false;
            return true;
        }

        matchedFromReview = reviews.Any(review =>
            review.Comment is not null
            && terms.Any(term => ContainsNormalized(review.Comment, term)));
        return matchedFromReview;
    }

    private static bool ContainsNormalized(string source, string value)
    {
        return source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static decimal? NormalizePositive(decimal? value)
    {
        return value is > 0 ? value : null;
    }
}
