using Homeji.Application.IRepositories.Activities;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
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
        UserActivityType? type,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.UserActivities
            .AsNoTracking()
            .Where(activity => activity.UserId == userId);
        if (type.HasValue)
        {
            query = query.Where(activity => activity.Type == type.Value);
        }

        return await query
            .OrderByDescending(activity => activity.OccurredAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetLatestByUserSinceAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken = default)
    {
        var rows = await _dbContext.UserActivities
            .AsNoTracking()
            .Where(activity => activity.OccurredAt >= since)
            .GroupBy(activity => activity.UserId)
            .Select(group => new
            {
                UserId = group.Key,
                LastSeenAt = group.Max(activity => activity.OccurredAt),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.UserId, row => row.LastSeenAt);
    }
}
