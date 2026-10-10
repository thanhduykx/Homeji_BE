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
    private const string AiHighlightTag = "Phù hợp theo dữ liệu tin";
    private const int MaxSearchTextLength = 1_000;
    private const int MaxCandidatePosts = 100;

    private static readonly Dictionary<string, string[]> CriterionSynonyms =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["parking"] = ["PARKING", "giu xe", "giữ xe", "xe", "parking"],
            ["freeTime"] = ["FREE_TIME", "gio giac", "giờ giấc", "tu do", "tự do", "free time"],
            ["wifi"] = ["WIFI", "internet", "wifi"],
            ["airConditioner"] = ["AIR_CONDITIONER", "may lanh", "máy lạnh", "dieu hoa", "điều hòa"],
            ["privateToilet"] = ["PRIVATE_BATHROOM", "wc rieng", "wc riêng", "ve sinh rieng", "vệ sinh riêng"],
            ["security"] = ["SECURITY", "an ninh", "bao ve", "bảo vệ"],
            ["quiet"] = ["QUIET", "yen tinh", "yên tĩnh"],
            ["petFriendly"] = ["PET_FRIENDLY", "thu cung", "thú cưng", "pet"],
            ["kitchen"] = ["KITCHEN", "bep", "bếp", "nau an", "nấu ăn"],
        };

    private readonly IAiSearchTextParser _parser;
    private readonly IRentalPostRepository _posts;
    private readonly IUserSubscriptionRepository _subscriptions;
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
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<AiParsedSearchCriteriaDto> ParseSearchAsync(
        AiParseSearchRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateText(request.Text);
        return await ParseNaturalLanguageAsync(text, null, cancellationToken);
    }

    private async Task<AiParsedSearchCriteriaDto> ParseNaturalLanguageAsync(
        string text, AiParsedSearchCriteriaDto? previous, CancellationToken cancellationToken)
    {
        try
        {
            var parsed = NormalizeParsedCriteria(await _parser.ParseAsync(text, cancellationToken));
            var interpreted = (previous ?? parsed) with
            {
                Location = parsed.Location ?? previous?.Location,
                Keyword = parsed.Keyword ?? previous?.Keyword,
                PriceMin = parsed.PriceMin ?? previous?.PriceMin,
                PriceMax = parsed.PriceMax ?? previous?.PriceMax,
                AreaMin = parsed.AreaMin ?? previous?.AreaMin,
                AreaMax = parsed.AreaMax ?? previous?.AreaMax,
                Criteria = (previous?.Criteria ?? []).Concat(parsed.Criteria).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            };
            var explicitCriteria = RentalSearchIntent.Apply(text, interpreted);
            // Gemini interprets language; verified user constraints remain authoritative for retrieval.
            return explicitCriteria with
            {
                Keyword = explicitCriteria.Destination is null ? parsed.Keyword ?? explicitCriteria.Keyword : null,
            };
        }
        catch (Exception error) when (error is ExternalDependencyException or ExternalServiceUnavailableException or HttpRequestException or System.Text.Json.JsonException || (error is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            throw new ExternalDependencyException("Gemini tạm thời không khả dụng. Bạn có thể dùng bộ lọc thông thường hoặc thử lại.");
        }
    }

    public async Task<AiHighlightResponseDto> HighlightRentalPostsAsync(
        AiHighlightRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.GroundedSearchEnabled) throw new ForbiddenAccessException("Tìm phòng theo nhu cầu hiện đang tắt. Bạn có thể dùng bộ lọc thường.");
        AiSearchTelemetry.Requests.Add(1);
        var text = ValidateText(request.Text);
        ValidatePreviousCriteria(request.PreviousCriteria);
        var parsed = NormalizeParsedCriteria(request.Criteria ?? await ParseNaturalLanguageAsync(
            text, request.PreviousCriteria is null ? null : NormalizeParsedCriteria(request.PreviousCriteria), cancellationToken));
        var maxResults = Math.Clamp(
            request.MaxResults <= 0 ? _options.MaxHighlightedPosts : request.MaxResults,
            1,
            Math.Clamp(_options.MaxHighlightedPosts, 1, 5));

        if (parsed.Unknown.Any(value => value is not ("feeUnits" or "commute" or "destination" or "motorcycleCoverage")))
            return new AiHighlightResponseDto(parsed, [], AiHighlightTag, null, null, null);

        var search = new RentalPostSearchDto(
            null, // Accent-normalized location/keyword matching is applied to the bounded candidate set.
            parsed.BudgetBasis == "total" ? null : parsed.PriceMin,
            parsed.PriceMax,
            parsed.AreaMin,
            parsed.AreaMax,
            10.7m,
            10.93m,
            106.72m,
            106.9m,
            parsed.RequiredAmenities,
            1,
            MaxCandidatePosts,
            MinAvailableSlots: parsed.Occupants,
            ExcludedAmenities: parsed.ExcludedAmenities,
            ExcludeRoommateShare: parsed.ExcludeRoommateShare,
            ExcludeSynthetic: true);

        var retrievalStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        var posts = await _posts.SearchActiveAsync(search, cancellationToken);
        AiSearchTelemetry.RetrievalDuration.Record(System.Diagnostics.Stopwatch.GetElapsedTime(retrievalStarted).TotalMilliseconds);
        var now = _timeProvider.GetUtcNow();
        var premiumByUserId = await _subscriptions.GetActivePremiumByUserIdsAsync(
            posts.Select(post => post.OwnerId).ToArray(),
            now,
            cancellationToken);
        var rankedPosts = posts
            .Where(post => MeetsConstraints(post, parsed))
            .Select(post =>
            {
                var isPremium = premiumByUserId.ContainsKey(post.OwnerId);
                var score = CalculateAiScore(post, parsed, out var reasons);
                var summary = RentalPostVisibility.ForPublicSearch(RentalPostMapper.ToSummaryDto(
                    post,
                    isPremium,
                    CalculateBoostScore(post, isPremium, now),
                    AiHighlightTag));
                var publicReasons = reasons.Select(reason => reason.Field == "address" ? reason with { Value = summary.Address } : reason).ToArray();

                var evidence = new List<AiRentalEvidenceDto>
                {
                    new(post.Id, "listing", "price", post.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new(post.Id, "listing", "address", summary.Address),
                    new(post.Id, "listing", "title", post.Title),
                    new(post.Id, "listing", "status", post.Status.ToString()),
                    new(post.Id, "listing", "coordinates", $"{summary.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{summary.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}"),
                };
                evidence.AddRange(post.Amenities.Where(amenity => parsed.RequiredAmenities.Contains(amenity.Code) || parsed.Criteria.Any(criterion => MatchesCriterion(post, criterion) && (CriterionSynonyms.TryGetValue(criterion, out var terms) ? terms : [criterion]).Contains(amenity.Code, StringComparer.OrdinalIgnoreCase)))
                    .Select(amenity => new AiRentalEvidenceDto(post.Id, "listing", "amenities", amenity.Code)));
                return new AiHighlightedRentalPostDto(summary, score, publicReasons.Select(reason => reason.Text).ToArray(), AiHighlightTag)
                {
                    Evidence = evidence,
                    ReasonEvidence = publicReasons,
                    UpdatedAt = post.UpdatedAt,
                    CommercialBoost = summary.BoostScore,
                    UnconfirmedConstraints = parsed.Unknown,
                };
            })
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Post.Price)
            .ThenBy(item => item.Post.Id)
            .Take(maxResults)
            .ToArray();

        var mapFocus = rankedPosts.FirstOrDefault()?.Post;
        AiSearchTelemetry.ResultCounts.Record(rankedPosts.Length);

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

    private static void ValidatePreviousCriteria(AiParsedSearchCriteriaDto? criteria)
    {
        if (criteria is null) return;
        if (criteria.Criteria is null || criteria.Criteria.Count > 20 || criteria.Criteria.Any(value => value is null || value.Length > 60) ||
            criteria.RequiredAmenities is null || criteria.RequiredAmenities.Count > 20 || criteria.RequiredAmenities.Any(code => !RentalSearchIntent.AmenityAliases.ContainsKey(code ?? "")) ||
            criteria.ExcludedAmenities is null || criteria.ExcludedAmenities.Count > 20 || criteria.ExcludedAmenities.Any(code => !RentalSearchIntent.AmenityAliases.ContainsKey(code ?? "")) ||
            criteria.Unknown is null || criteria.Unknown.Count > 10 || criteria.Unknown.Any(value => value is not ("budget" or "feeUnits" or "destination" or "commute" or "priceRange" or "motorcycleCoverage" or "area" or "occupants")) ||
            criteria.Location?.Length > 200 || criteria.Keyword?.Length > 200 || criteria.Destination?.Length > 200 ||
            criteria.PriceMin is <= 0 or > 1_000_000_000 || criteria.PriceMax is <= 0 or > 1_000_000_000 ||
            criteria.AreaMin is <= 0 or > 100_000 || criteria.AreaMax is <= 0 or > 100_000 ||
            criteria.Occupants is < 1 or > 20 || criteria.MaxCommuteMinutes is < 1 or > 240 || criteria.TravelMode is not (null or "DRIVING" or "WALKING" or "TRANSIT") || criteria.BudgetBasis is not ("rent" or "total"))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["previousCriteria"] = ["Tiêu chí cũ không hợp lệ. Hãy bắt đầu nhu cầu mới."] });
    }

    private static AiParsedSearchCriteriaDto NormalizeParsedCriteria(AiParsedSearchCriteriaDto criteria)
    {
        var normalizedCriteria = (criteria.Criteria ?? [])
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
            RequiredAmenities = NormalizeAmenityCodes(criteria.RequiredAmenities ?? []),
            ExcludedAmenities = NormalizeAmenityCodes(criteria.ExcludedAmenities ?? []),
            Unknown = (criteria.Unknown ?? []).Where(value => value is "budget" or "feeUnits" or "destination" or "commute" or "priceRange" or "motorcycleCoverage" or "area" or "occupants").Distinct().ToArray(),
            Occupants = criteria.Occupants is >= 1 and <= 20 ? criteria.Occupants : null,
            BudgetBasis = criteria.BudgetBasis == "total" ? "total" : "rent",
        };
    }

    private static decimal CalculateAiScore(
        RentalPost post,
        AiParsedSearchCriteriaDto criteria,
        out IReadOnlyCollection<AiRentalReasonDto> reasons)
    {
        var score = 0m;
        var resultReasons = new List<AiRentalReasonDto>();

        if (!string.IsNullOrWhiteSpace(criteria.Location)
            && (ContainsNormalized(post.Address, criteria.Location)
                || ContainsNormalized(post.Title, criteria.Location)))
        {
            score += 30;
            resultReasons.Add(new("Phù hợp khu vực người dùng yêu cầu.", post.Id, "listing", ContainsNormalized(post.Address, criteria.Location) ? "address" : "title", ContainsNormalized(post.Address, criteria.Location) ? post.Address : post.Title));
        }

        if (criteria.PriceMax.HasValue && post.Price <= criteria.PriceMax.Value)
        {
            score += 25;
            resultReasons.Add(new(criteria.BudgetBasis == "total" ? "Tiền thuê không vượt trần; tổng cả phí cần xác nhận." : "Giá nằm trong ngân sách tối đa.", post.Id, "listing", "price", post.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        else if (!criteria.PriceMax.HasValue)
        {
            score += 5;
        }

        if (criteria.BudgetBasis != "total" && criteria.PriceMin.HasValue && post.Price >= criteria.PriceMin.Value)
        {
            score += 5;
        }

        foreach (var criterion in criteria.Criteria.Concat(criteria.RequiredAmenities).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (MatchesCriterion(post, criterion))
            {
                score += 12;
                var terms = CriterionSynonyms.TryGetValue(criterion, out var aliases) ? aliases : [criterion];
                var code = post.Amenities.First(amenity => terms.Contains(amenity.Code, StringComparer.OrdinalIgnoreCase)).Code;
                resultReasons.Add(new($"Khớp tiện ích chủ tin khai báo: {code}.", post.Id, "listing", "amenities", code));
            }
        }

        if (resultReasons.Count == 0)
        {
            resultReasons.Add(new("Tin công khai trong phạm vi Homeji; chưa có thêm tiêu chí để xếp hạng.", post.Id, "listing", "status", post.Status.ToString()));
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
        string criterion)
    {
        var terms = CriterionSynonyms.TryGetValue(criterion, out var synonyms)
            ? synonyms
            : [criterion];

        // A substring in a negative review or prose is not evidence of an amenity.
        return terms.Any(term => post.Amenities.Any(amenity => amenity.Code.Equals(term, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ContainsNormalized(string source, string value)
    {
        return RentalSearchIntent.Normalize(source).Contains(RentalSearchIntent.Normalize(value), StringComparison.Ordinal);
    }

    private static string[] NormalizeAmenityCodes(IReadOnlyCollection<string> codes) => codes
        .Where(code => !string.IsNullOrWhiteSpace(code))
        .Select(code => code.Trim().ToUpperInvariant())
        .Where(RentalSearchIntent.AmenityAliases.ContainsKey)
        .Distinct(StringComparer.Ordinal).Take(20).ToArray();

    private static bool MeetsConstraints(RentalPost post, AiParsedSearchCriteriaDto criteria) =>
        post.Status == RentalPostStatus.Active && !post.IsSynthetic &&
        post.Latitude is >= 10.7m and <= 10.93m && post.Longitude is >= 106.72m and <= 106.9m &&
        (string.IsNullOrWhiteSpace(criteria.Location) || ContainsNormalized(post.Address, criteria.Location) || ContainsNormalized(post.Title, criteria.Location)) &&
        (string.IsNullOrWhiteSpace(criteria.Keyword) || ContainsNormalized(post.Address, criteria.Keyword) || ContainsNormalized(post.Title, criteria.Keyword) || ContainsNormalized(post.Description, criteria.Keyword)) &&
        (criteria.BudgetBasis == "total" || !criteria.PriceMin.HasValue || post.Price >= criteria.PriceMin) &&
        (!criteria.PriceMax.HasValue || post.Price <= criteria.PriceMax) &&
        (!criteria.AreaMin.HasValue || post.Area >= criteria.AreaMin) &&
        (!criteria.AreaMax.HasValue || post.Area <= criteria.AreaMax) &&
        (!criteria.Occupants.HasValue || post.AvailableSlots >= criteria.Occupants) &&
        (!criteria.ExcludeRoommateShare || post.Type != RentalPostType.RoommateShare) &&
        criteria.RequiredAmenities.All(code => post.Amenities.Any(amenity => amenity.Code == code)) &&
        criteria.ExcludedAmenities.All(code => post.Amenities.All(amenity => amenity.Code != code));

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized[..Math.Min(normalized.Length, 500)];
    }

    private static decimal? NormalizePositive(decimal? value)
    {
        return value is > 0 ? value : null;
    }
}
