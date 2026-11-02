using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RentalSourceListingRepository(ApplicationDbContext context, TimeProvider? timeProvider = null) : IRentalSourceListingRepository
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<IReadOnlyList<RentalSourceListing>> SearchAsync(RentalSourceSearchDto query, CancellationToken cancellationToken)
    {
        return await BuildSearchQuery(query).ToArrayAsync(cancellationToken);
    }

    internal IQueryable<RentalSourceListing> BuildSearchQuery(RentalSourceSearchDto query)
    {
        var now = _timeProvider.GetUtcNow();
        var listings = context.RentalSourceListings.AsNoTracking()
            .Where(listing => listing.SourceExpiresAt == null || listing.SourceExpiresAt > now);
        if (query.PropertyCategory is "room" or "apartment")
        {
            var apartments = context.RentalSourceListings.AsNoTracking().Where(listing =>
                EF.Functions.ILike(listing.Title, "%căn hộ%")
                || EF.Functions.ILike(listing.Title, "%can ho%")
                || EF.Functions.ILike(listing.Title, "%chung cư%")
                || EF.Functions.ILike(listing.Title, "%chung cu%")
                || EF.Functions.ILike(listing.Title, "%studio%")
                || EF.Functions.ILike(listing.Title, "%officetel%")
                || EF.Functions.ILike(listing.Title, "%apartment%")
                || EF.Functions.ILike(listing.Title, "%penthouse%")
                || EF.Functions.ILike(listing.Title, "%condotel%")
                || EF.Functions.ILike(listing.Title, "%duplex%"));
            listings = query.PropertyCategory == "apartment"
                ? listings.Where(listing => apartments.Any(apartment => apartment.Id == listing.Id))
                : listings.Where(listing => !apartments.Any(apartment => apartment.Id == listing.Id));
        }
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

        return listings.OrderByDescending(listing => listing.CollectedAt).ThenBy(listing => listing.Id)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
    }

    public Task<RentalSourceListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        return context.RentalSourceListings.AsNoTracking().SingleOrDefaultAsync(
            listing => listing.Id == id && (listing.SourceExpiresAt == null || listing.SourceExpiresAt > now), cancellationToken);
    }
}
