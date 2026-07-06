using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.SavedPosts;

namespace Homeji.Application.IServices.SavedPosts;

public interface ISavedPostService
{
    Task SaveAsync(Guid postId, CancellationToken cancellationToken = default);

    Task UnsaveAsync(Guid postId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RentalPostSummaryDto>> GetMineAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RoommateCandidateDto>> GetRoommateCandidatesAsync(Guid postId, CancellationToken cancellationToken = default);
}
