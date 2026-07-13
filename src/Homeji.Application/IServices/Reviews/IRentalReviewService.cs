using Homeji.Application.DTOs.Reviews;

namespace Homeji.Application.IServices.Reviews;

public interface IRentalReviewService
{
    Task<RentalReviewCollectionDto> GetByPostAsync(
        Guid rentalPostId,
        CancellationToken cancellationToken = default);

    Task<RentalReviewDto> UpsertAsync(
        Guid rentalPostId,
        UpsertRentalReviewDto request,
        CancellationToken cancellationToken = default);

    Task DeleteMineAsync(Guid rentalPostId, CancellationToken cancellationToken = default);
}
