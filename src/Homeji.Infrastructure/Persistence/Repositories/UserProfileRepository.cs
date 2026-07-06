using Homeji.Application.Abstractions.Persistence;
using Homeji.Domain.Profiles;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Persistence.Repositories;

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
        await _dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO homeji.user_profiles (id, display_name, created_at, updated_at)
             VALUES ({profile.Id}, {profile.DisplayName}, {profile.CreatedAt}, {profile.UpdatedAt})
             ON CONFLICT (id) DO UPDATE
             SET display_name = EXCLUDED.display_name,
                 updated_at = EXCLUDED.updated_at;
             """,
            cancellationToken);

        return await _dbContext.UserProfiles
            .AsNoTracking()
            .SingleAsync(existingProfile => existingProfile.Id == profile.Id, cancellationToken);
    }
}
