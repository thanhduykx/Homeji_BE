using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RentalPostRepository : IRentalPostRepository
{
    private const int MaxPageSize = 100;
    private readonly ApplicationDbContext _dbContext;

    public RentalPostRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RentalPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.RentalPosts.SingleOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public Task<RentalPost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.RentalPosts
            .Include(post => post.Media)
            .Include(post => post.Amenities)
            .SingleOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RentalPost>> SearchActiveAsync(
        RentalPostSearchDto search,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RentalPosts
            .AsNoTracking()
            .Include(post => post.Media)
            .Include(post => post.Amenities)
            .Where(post => post.Status == RentalPostStatus.Active);

        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = $"%{search.Keyword.Trim()}%";
            query = query.Where(post =>
                EF.Functions.ILike(post.Title, keyword)
                || EF.Functions.ILike(post.Description, keyword)
                || EF.Functions.ILike(post.Address, keyword));
        }

        if (search.MinPrice.HasValue)
        {
            query = query.Where(post => post.Price >= search.MinPrice.Value);
        }

        if (search.MaxPrice.HasValue)
        {
            query = query.Where(post => post.Price <= search.MaxPrice.Value);
        }

        if (search.MinArea.HasValue)
        {
            query = query.Where(post => post.Area >= search.MinArea.Value);
        }

        if (search.MaxArea.HasValue)
        {
            query = query.Where(post => post.Area <= search.MaxArea.Value);
        }

        if (search.MaxDeposit.HasValue)
        {
            query = query.Where(post => post.Deposit <= search.MaxDeposit.Value);
        }

        if (search.MinAvailableSlots.HasValue)
        {
            query = query.Where(post => post.AvailableSlots >= search.MinAvailableSlots.Value);
        }

        if (search.AvailableFromBefore.HasValue)
        {
            query = query.Where(post => post.AvailableFrom == null || post.AvailableFrom <= search.AvailableFromBefore.Value);
        }

        if (search.MinLatitude.HasValue)
        {
            query = query.Where(post => post.Latitude >= search.MinLatitude.Value);
        }

        if (search.MaxLatitude.HasValue)
        {
            query = query.Where(post => post.Latitude <= search.MaxLatitude.Value);
        }

        if (search.MinLongitude.HasValue)
        {
            query = query.Where(post => post.Longitude >= search.MinLongitude.Value);
        }

        if (search.MaxLongitude.HasValue)
        {
            query = query.Where(post => post.Longitude <= search.MaxLongitude.Value);
        }

        foreach (var amenity in search.Amenities.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim().ToUpperInvariant()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            query = query.Where(post => post.Amenities.Any(item => item.Code == amenity));
        }

        var page = Math.Max(1, search.Page);
        var pageSize = Math.Clamp(search.PageSize, 1, MaxPageSize);

        return await query
            .AsSplitQuery()
            .OrderByDescending(post => post.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalPost>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalPosts
            .AsNoTracking()
            .Include(post => post.Media)
            .Include(post => post.Amenities)
            .Where(post => post.Status == RentalPostStatus.Pending)
            .OrderBy(post => post.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalPost>> GetByOwnerAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalPosts
            .AsNoTracking()
            .Where(post => post.OwnerId == ownerId)
            .OrderByDescending(post => post.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalPost>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.RentalPosts
            .AsNoTracking()
            .Where(post => ids.Contains(post.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalPost>> GetByIdsWithMediaAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        return await _dbContext.RentalPosts
            .AsNoTracking()
            .Include(post => post.Media)
            .Include(post => post.Amenities)
            .Where(post => ids.Contains(post.Id))
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RentalPost post, CancellationToken cancellationToken = default)
    {
        await _dbContext.RentalPosts.AddAsync(post, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
