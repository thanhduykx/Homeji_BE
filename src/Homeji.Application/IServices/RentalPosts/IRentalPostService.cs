using Homeji.Application.DTOs.RentalPosts;

namespace Homeji.Application.IServices.RentalPosts;

public interface IRentalPostService
{
    Task<RentalPostDto> CreateDraftAsync(CreateRentalPostDraftDto request, CancellationToken cancellationToken = default);

    Task<RentalPostDto> UpdateAsync(Guid postId, UpdateRentalPostDto request, CancellationToken cancellationToken = default);

    Task<RentalPostDto> AddMediaAsync(Guid postId, AddRentalPostMediaDto request, CancellationToken cancellationToken = default);

    Task DeleteMediaAsync(Guid postId, Guid mediaId, CancellationToken cancellationToken = default);

    Task<RentalPostDto> SubmitAsync(Guid postId, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<RentalPostDto> GetDetailAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPostSummaryDto>> SearchAsync(RentalPostSearchDto request, CancellationToken cancellationToken = default);
}
