using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.Reviews;

public interface IRentalReviewRepository
{
    Task<RentalReview?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RentalReview?> GetByPostAndReviewerAsync(
        Guid rentalPostId,
        Guid reviewerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalReview>> GetByPostAsync(
        Guid rentalPostId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalReview>> GetByPostIdsAsync(
        IReadOnlyCollection<Guid> rentalPostIds,
        CancellationToken cancellationToken = default);

    Task AddAsync(RentalReview review, CancellationToken cancellationToken = default);

    void Remove(RentalReview review);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
