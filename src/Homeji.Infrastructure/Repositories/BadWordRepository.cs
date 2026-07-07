using Homeji.Application.IRepositories.Moderation;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class BadWordRepository : IBadWordRepository
{
    private readonly ApplicationDbContext _dbContext;

    public BadWordRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<string>> GetActiveValuesAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.BadWords
            .AsNoTracking()
            .Where(word => word.IsActive)
            .Select(word => word.Value)
            .ToListAsync(cancellationToken);
    }
}
