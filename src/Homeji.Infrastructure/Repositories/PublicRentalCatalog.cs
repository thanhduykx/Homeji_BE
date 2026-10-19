using Homeji.Application.Common;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class PublicRentalCatalog(ApplicationDbContext db, TimeProvider timeProvider) : IPublicRentalCatalog
{
    public async Task<PublicRentalPage?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await Visible().Include(post => post.Media).Include(post => post.Amenities)
            .AsSplitQuery().SingleOrDefaultAsync(post => post.Id == id, cancellationToken);
        if (post is null) return null;
        var visible = RentalPostVisibility.ForPublicSearch(RentalPostMapper.ToSummaryDto(post));
        return new PublicRentalPage(post.Id, visible.Title, post.Description, post.Price, post.Area,
            visible.Address, visible.ThumbnailPath, post.UpdatedAt, post.Type);
    }

    public async Task<PublicRentalSitemap> GetSitemapAsync(int page, CancellationToken cancellationToken)
    {
        const int pageSize = 1000;
        var query = Visible();
        var count = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(post => post.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(post => new PublicRentalSitemapItem(post.Id, post.UpdatedAt)).ToArrayAsync(cancellationToken);
        return new PublicRentalSitemap(items, Math.Max(1, (count + pageSize - 1) / pageSize));
    }

    private IQueryable<RentalPost> Visible()
    {
        var today = RentalPublicAvailability.Today(timeProvider);
        return db.RentalPosts.AsNoTracking().Where(post => post.Status == RentalPostStatus.Active && !post.IsSynthetic)
            .Where(post => post.Type != RentalPostType.RoomTransfer || post.OriginalLeaseEndsOn == null || post.OriginalLeaseEndsOn >= today);
    }
}
