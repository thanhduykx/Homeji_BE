using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RentalSourceListingRepository(ApplicationDbContext context) : IRentalSourceListingRepository
{
    public async Task<IReadOnlyList<RentalSourceListing>> SearchAsync(RentalSourceSearchDto query, CancellationToken cancellationToken)
    {
        var listings = context.RentalSourceListings.AsNoTracking();
        if (query.District is not null) listings = listings.Where(listing => listing.District == query.District);
        if (query.MinPrice.HasValue) listings = listings.Where(listing => listing.Price >= query.MinPrice.Value);
        if (query.MaxPrice.HasValue) listings = listings.Where(listing => listing.Price <= query.MaxPrice.Value);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            // Escape LIKE metacharacters so searches represent literal user text.
            var text = query.Keyword.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
            listings = listings.Where(listing => EF.Functions.ILike(listing.Title, $"%{text}%", "\\")
                || EF.Functions.ILike(listing.Address, $"%{text}%", "\\"));
        }

        return await listings.OrderByDescending(listing => listing.CollectedAt).ThenBy(listing => listing.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(cancellationToken);
    }

    public Task<RentalSourceListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.RentalSourceListings.AsNoTracking().SingleOrDefaultAsync(listing => listing.Id == id, cancellationToken);
}
