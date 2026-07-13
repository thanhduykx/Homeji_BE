using Homeji.Application.IRepositories.Activities;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class UserActivityRepository : IUserActivityRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserActivityRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(UserActivity activity, CancellationToken cancellationToken = default)
    {
        await _dbContext.UserActivities.AddAsync(activity, cancellationToken);
    }

    public async Task<IReadOnlyList<UserActivity>> GetForUserAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId)
            .OrderByDescending(activity => activity.OccurredAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
