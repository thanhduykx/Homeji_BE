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

    public async Task<IReadOnlyList<MarketplacePost>> SearchActiveAsync(
        string? keyword,
        string? category,
        decimal? minPrice,
        decimal? maxPrice,
        decimal? minLatitude,
        decimal? maxLatitude,
        decimal? minLongitude,
        decimal? maxLongitude,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.MarketplacePosts
            .AsNoTracking()
            .Include(post => post.Media)
            .Where(post => post.Status == MarketplacePostStatus.Active && post.AvailableQuantity > 0);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var pattern = $"%{keyword.Trim()}%";
            query = query.Where(post =>
                EF.Functions.ILike(post.Title, pattern)
                || EF.Functions.ILike(post.Description, pattern));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(post => post.Category == category.Trim());
        }

        if (minPrice.HasValue)
        {
            query = query.Where(post => post.Price >= minPrice.Value);
        }

        if (maxPrice.HasValue)
        {
            query = query.Where(post => post.Price <= maxPrice.Value);
        }

        if (minLatitude.HasValue && maxLatitude.HasValue && minLongitude.HasValue && maxLongitude.HasValue)
        {
            query = query.Where(post =>
                post.Latitude >= minLatitude.Value
                && post.Latitude <= maxLatitude.Value
                && post.Longitude >= minLongitude.Value
                && post.Longitude <= maxLongitude.Value);
        }

        return await query
            .OrderByDescending(post => post.UpdatedAt)
            .Skip(skip)
            .Take(take)
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
