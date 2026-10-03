using Homeji.Application.IRepositories.Marketplace;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class MarketplacePostRepository : IMarketplacePostRepository
{
    private readonly ApplicationDbContext _dbContext;

    public MarketplacePostRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MarketplacePost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.MarketplacePosts
            .Include(post => post.Media)
            .SingleOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public Task<MarketplacePost?> GetSellerLocationAnchorAsync(
        Guid sellerId,
        Guid? excludingPostId = null,
        CancellationToken cancellationToken = default) =>
        _dbContext.MarketplacePosts.AsNoTracking()
            .Where(post => post.SellerId == sellerId
                && (!excludingPostId.HasValue || post.Id != excludingPostId.Value))
            .OrderBy(post => post.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketplacePost>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplacePosts.AsNoTracking()
            .Include(post => post.Media)
            .Where(post => ids.Contains(post.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketplacePost>> GetByIdsForUpdateAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default) =>
        await _dbContext.MarketplacePosts
            .Include(post => post.Media)
            .Where(post => ids.Contains(post.Id))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MarketplacePost>> SearchActiveAsync(
        MarketplaceSearchQuery search,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MarketplacePosts
            .AsNoTracking()
            .Where(post => post.Status == MarketplacePostStatus.Active && post.AvailableQuantity > 0);
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var pattern = $"%{search.Keyword.Trim()}%";
            query = query.Where(post =>
                EF.Functions.ILike(post.Title, pattern)
                || EF.Functions.ILike(post.Description, pattern));
        }

        if (!string.IsNullOrWhiteSpace(search.Category))
        {
            query = query.Where(post => post.Category == search.Category.Trim());
        }

        if (search.SellerId.HasValue)
        {
            query = query.Where(post => post.SellerId == search.SellerId.Value);
        }

        if (search.ListingType.HasValue)
        {
            query = query.Where(post => post.ListingType == search.ListingType.Value);
        }

        if (search.MinPrice.HasValue)
        {
            query = query.Where(post => post.Price >= search.MinPrice.Value);
        }

        if (search.MaxPrice.HasValue)
        {
            query = query.Where(post => post.Price <= search.MaxPrice.Value);
        }

        if (search.MinLatitude.HasValue && search.MaxLatitude.HasValue && search.MinLongitude.HasValue && search.MaxLongitude.HasValue)
        {
            query = query.Where(post =>
                post.Latitude >= search.MinLatitude.Value
                && post.Latitude <= search.MaxLatitude.Value
                && post.Longitude >= search.MinLongitude.Value
                && post.Longitude <= search.MaxLongitude.Value);
        }

        if (search.CenterLatitude.HasValue && search.CenterLongitude.HasValue && search.RadiusKm.HasValue)
        {
            const double radiansPerDegree = Math.PI / 180;
            const double earthRadiusKm = 6371;
            var latitude = (double)search.CenterLatitude.Value;
            var longitude = (double)search.CenterLongitude.Value;
            var limit = Math.Pow(Math.Sin((double)search.RadiusKm.Value / (2 * earthRadiusKm)), 2);
            // Haversine's component is monotonic with distance; filter and sort in SQL before paging.
            return await query.Select(post => new
                {
                    Post = post,
                    DistanceComponent = Math.Pow(Math.Sin(((double)post.Latitude - latitude) * radiansPerDegree / 2), 2)
                        + Math.Cos(latitude * radiansPerDegree) * Math.Cos((double)post.Latitude * radiansPerDegree)
                        * Math.Pow(Math.Sin(((double)post.Longitude - longitude) * radiansPerDegree / 2), 2),
                })
                .Where(item => item.DistanceComponent <= limit)
                .OrderBy(item => item.DistanceComponent)
                .ThenByDescending(item => item.Post.UpdatedAt)
                .ThenBy(item => item.Post.Id)
                .Skip(search.Skip)
                .Take(search.Take)
                .Select(item => item.Post)
                .Include(post => post.Media)
                .ToListAsync(cancellationToken);
        }

        return await query
            .OrderByDescending(post => post.UpdatedAt)
            .ThenBy(post => post.Id)
            .Skip(search.Skip)
            .Take(search.Take)
            .Include(post => post.Media)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MarketplacePost post, CancellationToken cancellationToken = default)
    {
        await _dbContext.MarketplacePosts.AddAsync(post, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
