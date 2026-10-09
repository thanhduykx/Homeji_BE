using Homeji.Application.IRepositories.Roommates;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
namespace Homeji.Infrastructure.Repositories;
public sealed class RoommateDirectoryRepository(ApplicationDbContext db) : IRoommateDirectoryRepository
{
    public Task<RoommateProfile?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        db.RoommateProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, cancellationToken);
    public async Task SaveAsync(RoommateProfile profile, CancellationToken cancellationToken = default)
    {
        var existing = await db.RoommateProfiles.FindAsync([profile.UserId], cancellationToken);
        if (existing is null) db.RoommateProfiles.Add(profile);
        else db.Entry(existing).CurrentValues.SetValues(profile);
        await db.SaveChangesAsync(cancellationToken);
    }
    public async Task<(IReadOnlyList<(UserProfile User, RoommateProfile Profile)> Items, int Total)> SearchAsync(
        Guid excludedUserId, RoommateIntent? intent, string? keyword, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = from profile in db.RoommateProfiles.AsNoTracking()
                    join user in db.UserProfiles.AsNoTracking() on profile.UserId equals user.Id
                    where profile.IsDiscoverable && user.Role == UserRole.Renter && user.Id != excludedUserId
                    select new { User = user, Profile = profile };
        if (intent.HasValue) query = query.Where(x => x.Profile.Intent == intent.Value);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var term = "%" + keyword.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
            query = query.Where(x => EF.Functions.ILike(x.User.DisplayName, term)
                || (x.User.School != null && EF.Functions.ILike(x.User.School, term))
                || (x.User.PreferredArea != null && EF.Functions.ILike(x.User.PreferredArea, term)));
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(x => x.Profile.UpdatedAt).ThenBy(x => x.User.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return (items.Select(x => (x.User, x.Profile)).ToArray(), total);
    }
}
