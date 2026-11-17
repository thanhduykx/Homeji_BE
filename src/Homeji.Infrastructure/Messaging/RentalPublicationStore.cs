using Homeji.Application.Common.Exceptions;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.Messaging;

internal sealed class RentalPublicationStore(
    ApplicationDbContext db,
    IOptions<RentalPublicationKafkaOptions> options) : IRentalPublicationStore
{
    public async Task SaveApprovalAsync(RentalPost post, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled || post.IsSynthetic)
        {
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var previousUpdate = db.Entry(post).Property(rentalPost => rentalPost.UpdatedAt).OriginalValue;
        var publication = new RentalPublication
        {
            Id = Guid.NewGuid(),
            RentalPostId = post.Id,
            OccurredAt = post.UpdatedAt,
            AvailableAt = post.UpdatedAt
        };
        db.RentalPublications.Add(publication);

        await db.Database.CreateExecutionStrategy().ExecuteInTransactionAsync(
            async token =>
            {
                // Hold the row only during the final save. A second moderator may have loaded the same pending post.
                var eligible = await db.Database.SqlQuery<int>($"""
                    SELECT 1 AS "Value" FROM homeji.rental_posts
                    WHERE id = {post.Id} AND status = {(int)RentalPostStatus.Pending} AND updated_at = {previousUpdate}
                    FOR UPDATE
                    """).ToListAsync(token);
                if (eligible.Count == 0)
                {
                    throw new ConflictException("Bài đăng đã thay đổi trong lúc duyệt. Hãy tải lại rồi thử lại.");
                }

                await db.SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
            },
            token => db.RentalPublications.AnyAsync(item => item.Id == publication.Id, token),
            cancellationToken);

        db.ChangeTracker.AcceptAllChanges();
    }
}
