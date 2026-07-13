using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RentalWantedPostRepository : IRentalWantedPostRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RentalWantedPostRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RentalWantedPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.RentalWantedPosts.SingleOrDefaultAsync(post => post.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RentalWantedPost>> SearchActiveAsync(
        string? area,
        decimal? maxBudget,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.RentalWantedPosts.AsNoTracking().Where(post => post.Status == WantedPostStatus.Active);
        if (!string.IsNullOrWhiteSpace(area))
        {
            var pattern = $"%{area.Trim()}%";
            query = query.Where(post => EF.Functions.ILike(post.PreferredArea, pattern));
        }

        if (maxBudget.HasValue)
        {
            query = query.Where(post => post.MaxBudget <= maxBudget.Value);
        }

        return await query.OrderByDescending(post => post.UpdatedAt).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RentalWantedPost post, CancellationToken cancellationToken = default)
    {
        await _dbContext.RentalWantedPosts.AddAsync(post, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
