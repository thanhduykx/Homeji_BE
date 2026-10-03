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
        if (query.Page is < 1 or > 1000 || query.PageSize is < 1 or > 50
            || query.Keyword?.Length > 200
            || query.District is not (null or "quan-9" or "thu-duc")
            || query.MinPrice < 0 || query.MaxPrice < 0 || query.MinPrice > query.MaxPrice)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["query"] = ["Bộ lọc không hợp lệ. Khu vực chỉ gồm quan-9 hoặc thu-duc; pageSize từ 1 đến 50."],
            });
        }

        var records = await repository.SearchAsync(query with { Keyword = query.Keyword?.Trim() }, cancellationToken);
        return records.Select(ToDto).ToArray();
    }

    public async Task<RentalSourceListingDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var listing = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalSourceListing), id);
        return ToDto(listing);
    }

    private static RentalSourceListingDto ToDto(RentalSourceListing listing) => new(
        listing.Id, listing.Source, listing.SourceId, listing.SourceUrl, listing.Title,
        listing.Address, listing.District, listing.Price, listing.Area, listing.ImageUrls.ToArray(),
        listing.SourceUpdatedAt, listing.CollectedAt);
}
