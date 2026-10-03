using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;

namespace Homeji.Application.IRepositories.RentalPosts;

public interface IRentalSourceListingRepository
{
    Task<IReadOnlyList<RentalSourceListing>> SearchAsync(RentalSourceSearchDto query, CancellationToken cancellationToken);
    Task<RentalSourceListing?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
