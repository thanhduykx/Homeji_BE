using Homeji.Domain.Enums;

namespace Homeji.Application.IServices.RentalPosts;

public interface IPublicRentalCatalog
{
    Task<PublicRentalPage?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<PublicRentalSitemap> GetSitemapAsync(int page, CancellationToken cancellationToken);
}

public sealed record PublicRentalPage(Guid Id, string Title, string Description, decimal Price, decimal Area,
    string Address, string? Image, DateTimeOffset UpdatedAt, RentalPostType Type);
public sealed record PublicRentalSitemapItem(Guid Id, DateTimeOffset UpdatedAt);
public sealed record PublicRentalSitemap(IReadOnlyList<PublicRentalSitemapItem> Items, int TotalPages);
