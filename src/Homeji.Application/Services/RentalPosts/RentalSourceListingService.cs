using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.RentalPosts;

public sealed class RentalSourceListingService(IRentalSourceListingRepository repository) : IRentalSourceListingService
{
    public async Task<IReadOnlyList<RentalSourceListingDto>> SearchAsync(
        RentalSourceSearchDto query, CancellationToken cancellationToken)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 50
            || ((long)query.Page - 1) * query.PageSize > int.MaxValue
            || query.Keyword?.Length > 200
            || query.District is not (null or "quan-9" or "thu-duc")
            || query.PropertyCategory is not (null or "room" or "apartment")
            || query.MinPrice < 0 || query.MaxPrice < 0 || query.MinPrice > query.MaxPrice)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["query"] = ["Bộ lọc không hợp lệ. Khu vực chỉ gồm quan-9 hoặc thu-duc; pageSize từ 1 đến 50."],
            });
        }

        var records = await repository.SearchAsync(query with { Keyword = query.Keyword?.Trim() }, cancellationToken);
        return records.Select(listing => ToDto(listing)).ToArray();
    }

    public async Task<RentalSourceListingDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalSourceListing), id);
        return ToDto(listing, true);
    }

    public async Task<RentalSourceContactDto> GetContactAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalSourceListing), id);
        var phones = listing.ContactPhones.Length > 0
            ? listing.ContactPhones
            : listing.ContactPhone is { Length: > 0 } phone ? [phone] : [];
        return new(listing.ContactPhone) { Phones = phones };
    }

    public async Task<Homeji.Application.DTOs.Chatbot.ChatbotSourceMatchDto> MatchAsync(
        Homeji.Application.DTOs.AI.AiParsedSearchCriteriaDto criteria, int limit, CancellationToken cancellationToken)
    {
        const int pageSize = 50, maxPages = 20;
        var listings = new List<RentalSourceListing>();
        // Description-based amenity and accent-insensitive matching cannot run in SQL, so filter price there and the rest here.
        var minPrice = criteria.BudgetBasis == "total" ? null : criteria.PriceMin;
        for (var page = 1; page <= maxPages; page++)
        {
            var rows = await repository.SearchAsync(new RentalSourceSearchDto(null, null, minPrice, criteria.PriceMax, page, pageSize), cancellationToken);
            listings.AddRange(rows);
            if (rows.Count < pageSize) break;
        }
        return RentalSourceMatcher.Match(listings, criteria, limit);
    }

    private static RentalSourceListingDto ToDto(RentalSourceListing listing, bool details = false)
    {
        var facts = RentalSourceFacts.Parse(listing.Description);
        return new(
            listing.Id, listing.Source, listing.SourceId, listing.SourceUrl, listing.Title,
            listing.Address, listing.District, listing.Price, listing.Area, listing.ImageUrls.ToArray(),
            listing.SourceUpdatedAt, listing.CollectedAt)
        {
            Latitude = listing.Latitude,
            PublisherName = listing.PublisherName,
            Description = details ? RentalSourceFacts.RemovePublicContactNumbers(listing.Description) : null,
            ContactPhone = null,
            DetailsCollectedAt = listing.DetailsCollectedAt,
            Longitude = listing.Longitude,
            SourceExpiresAt = listing.SourceExpiresAt,
            SourceCheckedAt = listing.SourceCheckedAt,
            DepositLabel = facts.DepositLabel,
            AmenityHighlights = facts.Amenities,
        };
    }
}
