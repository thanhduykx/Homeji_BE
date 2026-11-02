using Homeji.Application.DTOs.RentalPosts;

namespace Homeji.Application.IServices.RentalPosts;

public interface IRentalSourceListingService
{
    Task<IReadOnlyList<RentalSourceListingDto>> SearchAsync(RentalSourceSearchDto query, CancellationToken cancellationToken);
    Task<RentalSourceListingDto> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<RentalSourceContactDto> GetContactAsync(Guid id, CancellationToken cancellationToken);
}
