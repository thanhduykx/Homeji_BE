using Homeji.Application.DTOs.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.RentalPosts;

public interface IRentalPostRepository
{
    Task<RentalPost?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RentalPost?> GetByIdWithMediaAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPost>> SearchActiveAsync(
        RentalPostSearchDto search,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPost>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPost>> GetByOwnerAsync(
        Guid ownerId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPost>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    Task AddAsync(RentalPost post, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
