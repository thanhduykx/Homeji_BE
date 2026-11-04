using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.Services.AI;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.RentalPosts;

/// <summary>
/// Applies parsed chat criteria to public source listings. Amenities count only when the
/// listing description states them, so a missing mention never passes a required filter.
/// </summary>
public static class RentalSourceMatcher
{
    private const int ExcerptLength = 300;
    private static readonly string[] SearchableCriteriaUnknowns = ["feeUnits", "commute", "destination", "motorcycleCoverage"];
    private static readonly HashSet<string> KeywordStopWords = new(StringComparer.Ordinal)
    {
        "phong", "tro", "nha", "cho", "thue", "gia", "re", "can", "tim", "gan", "o", "khu", "vuc", "o ghep",
    };

    public static ChatbotSourceMatchDto Match(IEnumerable<RentalSourceListing> listings, AiParsedSearchCriteriaDto criteria, int limit)
    {
        if (limit <= 0 || criteria.Unknown.Any(value => !SearchableCriteriaUnknowns.Contains(value)))
            return new([], 0);

        var keywordTokens = KeywordTokens(criteria.Keyword);
        var locationParts = LocationParts(criteria.Location);
        var matches = listings
            .Select(listing => (Listing: listing, Codes: RentalSourceFacts.AmenityCodes(listing.Description)))
            .Where(item => Meets(item.Listing, item.Codes, criteria, locationParts, keywordTokens))
            .Select(item => (item.Listing, Score: Score(item.Codes, criteria, locationParts.Count > 0)))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Listing.SourceUpdatedAt ?? item.Listing.CollectedAt)
            .ThenBy(item => item.Listing.Price)
            .ThenBy(item => item.Listing.Id)
            .ToArray();

        return new(matches.Take(limit).Select(item => ToDto(item.Listing)).ToArray(), matches.Length);
    }

    private static bool Meets(RentalSourceListing listing, IReadOnlySet<string> codes, AiParsedSearchCriteriaDto criteria,
        List<string> locationParts, List<string> keywordTokens)
    {
        if (criteria.PriceMax.HasValue && listing.Price > criteria.PriceMax) return false;
        if (criteria.BudgetBasis != "total" && criteria.PriceMin.HasValue && listing.Price < criteria.PriceMin) return false;
        if (criteria.AreaMin.HasValue && listing.Area < criteria.AreaMin) return false;
        if (criteria.AreaMax.HasValue && listing.Area > criteria.AreaMax) return false;
        if (!criteria.RequiredAmenities.All(codes.Contains) || criteria.ExcludedAmenities.Any(codes.Contains)) return false;

        var place = RentalSearchIntent.Normalize($"{listing.Title} {listing.Address} {DistrictLabel(listing.District)}");
        if (criteria.ExcludeRoommateShare && place.Contains("o ghep", StringComparison.Ordinal)) return false;
        if (!locationParts.All(part => place.Contains(part, StringComparison.Ordinal))) return false;
        if (keywordTokens.Count == 0) return true;
        var text = place + " " + RentalSearchIntent.Normalize(listing.Description ?? string.Empty);
        return keywordTokens.All(token => text.Contains(token, StringComparison.Ordinal));
    }

    private static int Score(IReadOnlySet<string> codes, AiParsedSearchCriteriaDto criteria, bool locationMatched)
    {
        var preferred = criteria.Criteria.Select(RentalSearchIntent.CanonicalAmenity).Count(codes.Contains);
        return (locationMatched ? 30 : 0) + criteria.RequiredAmenities.Count * 12 + preferred * 12;
    }

    private static ChatbotSourceRoomDto ToDto(RentalSourceListing listing)
    {
        var facts = RentalSourceFacts.Parse(listing.Description);
        return new(listing.Id, listing.Title, listing.Address, listing.Price, listing.Area,
            facts.Amenities, facts.DepositLabel, Excerpt(listing.Description));
    }

    private static string? Excerpt(string? description)
    {
        var text = RentalSourceFacts.RemovePublicContactNumbers(description);
        if (string.IsNullOrWhiteSpace(text)) return null;
        var flat = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return flat.Length <= ExcerptLength ? flat : flat[..ExcerptLength].TrimEnd() + "…";
    }

    private static List<string> LocationParts(string? location) =>
        string.IsNullOrWhiteSpace(location) ? [] : location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(RentalSearchIntent.Normalize).ToList();

    private static List<string> KeywordTokens(string? keyword) =>
        string.IsNullOrWhiteSpace(keyword) ? [] : RentalSearchIntent.Normalize(keyword)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => !KeywordStopWords.Contains(token)).Distinct(StringComparer.Ordinal).ToList();

    private static string DistrictLabel(string district) => district switch
    {
        "quan-9" => "Quận 9",
        "thu-duc" => "Thủ Đức",
        _ => string.Empty,
    };
}
