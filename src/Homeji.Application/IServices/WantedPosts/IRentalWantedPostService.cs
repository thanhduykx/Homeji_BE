using Homeji.Application.DTOs.WantedPosts;

namespace Homeji.Application.IServices.WantedPosts;

public interface IRentalWantedPostService
{
    Task<RentalWantedPostDto> CreateAsync(UpsertRentalWantedPostDto request, CancellationToken cancellationToken = default);
    Task<RentalWantedPostDto> UpdateAsync(Guid id, UpsertRentalWantedPostDto request, CancellationToken cancellationToken = default);
    Task<RentalWantedPostDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RentalWantedPostDto>> SearchAsync(string? area, decimal? maxBudget, int page, int pageSize, CancellationToken cancellationToken = default);
    Task CloseAsync(Guid id, CancellationToken cancellationToken = default);
}
