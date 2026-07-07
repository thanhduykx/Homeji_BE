using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class SavedPostRepository : ISavedPostRepository
{
    private readonly ApplicationDbContext _dbContext;

    public SavedPostRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(Guid userId, Guid rentalPostId, CancellationToken cancellationToken = default)
    {
        return _dbContext.SavedPosts.AnyAsync(
            saved => saved.UserId == userId && saved.RentalPostId == rentalPostId,
            cancellationToken);
    }

    public async Task AddAsync(SavedPost savedPost, CancellationToken cancellationToken = default)
    {
        await _dbContext.SavedPosts.AddAsync(savedPost, cancellationToken);
    }

    public async Task<bool> RemoveAsync(Guid userId, Guid rentalPostId, CancellationToken cancellationToken = default)
    {
        var savedPost = await _dbContext.SavedPosts.SingleOrDefaultAsync(
            saved => saved.UserId == userId && saved.RentalPostId == rentalPostId,
            cancellationToken);

        if (savedPost is null)
        {
            return false;
        }

        _dbContext.SavedPosts.Remove(savedPost);
        return true;
    }

    public async Task<IReadOnlyList<SavedPost>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SavedPosts
            .AsNoTracking()
            .Where(saved => saved.UserId == userId)
            .OrderByDescending(saved => saved.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SavedPost>> GetByPostAsync(Guid rentalPostId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.SavedPosts
            .AsNoTracking()
            .Where(saved => saved.RentalPostId == rentalPostId)
            .OrderByDescending(saved => saved.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
