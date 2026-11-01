using Homeji.Application.Services.Subscriptions;
namespace Homeji.Application.IServices.Subscriptions;

public sealed record PremiumPriceAnnouncement(Guid Id, string PackageCode, string PackageName, decimal PreviousPrice, decimal Price, string Reason, DateTimeOffset CreatedAt);
public sealed record ChangePremiumPriceRequest(Guid RequestId, decimal ExpectedPrice, decimal Price, string Reason);

public interface IPremiumPricing
{
    Task<IReadOnlyList<PremiumPlanOptions>> GetPlansAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PremiumPriceAnnouncement>> GetAnnouncementsAsync(CancellationToken cancellationToken = default);
    Task<PremiumPriceAnnouncement> ChangeAsync(string code, ChangePremiumPriceRequest request, CancellationToken cancellationToken = default);
}
