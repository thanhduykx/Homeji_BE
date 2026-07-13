using Homeji.Application.IRepositories.Reviews;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class RentalReviewRepository : IRentalReviewRepository
{
    private readonly ApplicationDbContext _dbContext;

    public RentalReviewRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<RentalReview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.RentalReviews.AsNoTracking().SingleOrDefaultAsync(review => review.Id == id, cancellationToken);
    }

    public Task<RentalReview?> GetByPostAndReviewerAsync(
        Guid rentalPostId,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RentalReviews.SingleOrDefaultAsync(
            review => review.RentalPostId == rentalPostId && review.ReviewerId == reviewerId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RentalReview>> GetByPostAsync(
        Guid rentalPostId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.RentalReviews
            .AsNoTracking()
            .Where(review => review.RentalPostId == rentalPostId)
            .OrderByDescending(review => review.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RentalReview>> GetByPostIdsAsync(
        IReadOnlyCollection<Guid> rentalPostIds,
        CancellationToken cancellationToken = default)
    {
        if (rentalPostIds.Count == 0)
        {
            return [];
        }

        return await _dbContext.RentalReviews
            .AsNoTracking()
            .Where(review => rentalPostIds.Contains(review.RentalPostId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(RentalReview review, CancellationToken cancellationToken = default)
    {
        await _dbContext.RentalReviews.AddAsync(review, cancellationToken);
    }

    public void Remove(RentalReview review)
    {
        _dbContext.RentalReviews.Remove(review);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
