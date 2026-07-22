using Homeji.Application.IRepositories.Profiles;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Homeji.Domain.Enums;

namespace Homeji.Infrastructure.Repositories;

public sealed class UserProfileRepository : IUserProfileRepository
{
    private readonly ApplicationDbContext _dbContext;

    public UserProfileRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<UserProfile?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.UserProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(profile => profile.Id == userId, cancellationToken);
    }

    public async Task<UserProfile> UpsertAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        var existingProfile = await _dbContext.UserProfiles
            .SingleOrDefaultAsync(item => item.Id == profile.Id, cancellationToken);

        if (existingProfile is null)
        {
            _dbContext.UserProfiles.Add(profile);
        }
        else
        {
            _dbContext.Entry(existingProfile).CurrentValues.SetValues(profile);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await _dbContext.UserProfiles
            .AsNoTracking()
            .SingleAsync(existingProfile => existingProfile.Id == profile.Id, cancellationToken);
    }

    public async Task<UserProfile> SaveAsync(
        UserProfile profile,
        CancellationToken cancellationToken = default)
    {
        _dbContext.UserProfiles.Update(profile);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<IReadOnlyList<UserProfile>> GetByIdsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => userIds.Contains(profile.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.UserProfiles
            .AsNoTracking()
            .OrderBy(profile => profile.Id)
            .Select(profile => profile.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(
        string address,
        decimal price,
        Guid excludedUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var candidates = await _dbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id != excludedUserId
                && profile.Role == UserRole.Renter
                && (profile.MaxBudget == null || profile.MaxBudget >= price))
            .OrderByDescending(profile => profile.UpdatedAt)
            .Take(Math.Max(take, 500))
            .ToListAsync(cancellationToken);

        return candidates
            .Where(profile => string.IsNullOrWhiteSpace(profile.PreferredArea)
                || address.Contains(profile.PreferredArea, StringComparison.OrdinalIgnoreCase))
            .Take(take)
            .ToArray();
    }
}
