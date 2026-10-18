using Homeji.Application.DTOs.RentalPosts;
namespace Homeji.Application.IServices.RentalPosts;

public interface ISearchInterestService
{
    Task<InterestOverview> GetAsync(CancellationToken ct);
    Task<InterestOverview> SettingsAsync(InterestSettings request, CancellationToken ct);
    Task<InterestOverview> ObserveAsync(ObserveInterest request, CancellationToken ct);
    Task<InterestOverview> FollowAsync(InterestCriteria request, CancellationToken ct);
    Task<InterestOverview> RemoveAsync(Guid id, CancellationToken ct);
    Task<InterestOverview> DismissAsync(string area, CancellationToken ct);
    Task<InterestOverview> ClearAsync(CancellationToken ct);
    Task<bool> ViewedAsync(Guid postId, CancellationToken ct);
    Task<AiInterestProposal> InferAsync(CancellationToken ct);
    Task<InterestOverview> RankAsync(CancellationToken ct);
    Task ProcessBatchAsync(CancellationToken ct);
}
