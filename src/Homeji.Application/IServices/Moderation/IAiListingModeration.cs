using Homeji.Application.DTOs.Moderation;
namespace Homeji.Application.IServices.Moderation;
public interface IAiListingModeration
{
    Task TickAsync(CancellationToken cancellationToken);
    Task<AiReviewDashboardDto> GetDashboardAsync(CancellationToken cancellationToken);
    Task UpdateSettingsAsync(AiReviewSettingsDto settings, CancellationToken cancellationToken);
    Task QueueBackfillAsync(CancellationToken cancellationToken);
    Task ResolveAsync(Guid reviewId, bool restore, CancellationToken cancellationToken);
}
public interface IAiListingReviewClient
{
    Task<AiReviewVerdict> ReviewAsync(string snapshot, CancellationToken cancellationToken);
}
