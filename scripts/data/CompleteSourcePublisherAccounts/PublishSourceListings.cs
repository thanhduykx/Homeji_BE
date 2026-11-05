using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static class PublishSourceListings
{
    public static async Task RunAsync(NpgsqlConnection connection, Guid[] importedAccountIds, bool apply)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        var profiles = await db.UserProfiles.AsNoTracking().Where(profile => importedAccountIds.Contains(profile.Id)).ToArrayAsync();
        var owners = profiles.Where(profile => !string.IsNullOrWhiteSpace(profile.Phone)).GroupBy(profile => profile.Phone!)
            .Where(group => group.Count() == 1).ToDictionary(group => group.Key, group => group.Single());
        var now = DateTimeOffset.UtcNow;
        var sources = await db.RentalSourceListings.AsNoTracking().Where(source => source.SourceExpiresAt == null || source.SourceExpiresAt > now).ToArrayAsync();
        var existing = await db.RentalPosts.AsNoTracking().Select(post => new { post.Id, post.OwnerId, post.Status }).ToDictionaryAsync(post => post.Id);
        var candidates = new List<(RentalSourceListing Source, UserProfile Owner, RentalPost Post)>();
        var manifest = new List<object>();
        var skippedNoLocation = 0; var skippedImages = 0; var alreadyLinked = 0;
        foreach (var source in sources)
        {
            if (source.ContactPhone == null || !owners.TryGetValue(source.ContactPhone, out var owner)) continue;
            if (existing.TryGetValue(source.Id, out var native))
            {
                if (native.OwnerId != owner.Id) throw new InvalidOperationException("Source ID collision with an unrelated native owner; no changes applied.");
                alreadyLinked++;
                manifest.Add(new { sourceId = source.Id, ownerId = owner.Id, nativePostId = native.Status == RentalPostStatus.Active ? (Guid?)source.Id : null });
                continue;
            }
            var noLocation = source.Latitude == null || source.Longitude == null || source.Latitude is < 10m or > 12m || source.Longitude is < 105m or > 108m;
            if (noLocation) skippedNoLocation++;
            var photos = source.ImageUrls.Distinct().Select((url, index) => new UriBuilder(url) { Fragment = $"homeji-source-{source.Id:N}-{index}" }.Uri.AbsoluteUri).ToArray();
            var insufficientPhotos = photos.Length < RentalPost.MinimumImageCountForSubmit || photos.Length > 10 || photos.Any(url => url.Length > RentalPostMedia.MaxPathLength);
            if (insufficientPhotos) skippedImages++;
            var post = RentalPost.CreateDraft(owner.Id, RentalPostType.VacantRoom, now);
            db.Entry(post).Property(value => value.Id).CurrentValue = source.Id;
            var description = Regex.Replace(source.Description ?? source.Title, @"(?<!\d)(?:0(?:[\s().-]*\d){9,10}|\+?84(?:[\s().-]*\d){9})(?!\d)", "[số liên hệ ẩn]");
            var provenance = $"\n\nTin được nhập từ {source.SourceUrl}. Số người tối đa và các khoản phí chưa ghi rõ cần xác nhận với người đăng.";
            description = description[..Math.Min(description.Length, RentalPost.MaxDescriptionLength - provenance.Length)] + provenance;
            if (!noLocation)
                post.UpdateDetails(RentalPostType.VacantRoom, source.Title, description, source.Price, 0, source.Area,
                    source.Address, source.Latitude!.Value, source.Longitude!.Value, [], now);
            else
            {
                // Partial import only: an incomplete draft retains the domain's unset location.
                // It is never submitted, approved or exposed by the public native endpoint.
                var entry = db.Entry(post);
                entry.Property(value => value.Title).CurrentValue = source.Title;
                entry.Property(value => value.Description).CurrentValue = description;
                entry.Property(value => value.Price).CurrentValue = source.Price;
                entry.Property(value => value.Area).CurrentValue = source.Area;
                entry.Property(value => value.Address).CurrentValue = source.Address;
            }
            var validPhotos = photos.Where(url => url.Length <= RentalPostMedia.MaxPathLength).Take(10).ToArray();
            for (var index = 0; index < validPhotos.Length; index++) post.AddMedia(MediaType.Image, RentalPostMedia.ImportedSourceBucket, validPhotos[index], index == 0, index, now);
            if (!noLocation && !insufficientPhotos) { post.Submit(now); post.Approve(now); }
            candidates.Add((source, owner, post));
            manifest.Add(new { sourceId = source.Id, ownerId = owner.Id, nativePostId = post.Status == RentalPostStatus.Active ? (Guid?)source.Id : null });
        }
        var summary = new { apply, readyToPublish = candidates.Count(row => row.Post.Status == RentalPostStatus.Active), incompleteDrafts = candidates.Count(row => row.Post.Status == RentalPostStatus.Draft), alreadyLinked, skippedNoLocation, skippedImages, sourceRecordsPreserved = sources.Length };
        Directory.CreateDirectory("output/source-publisher-accounts");
        await File.WriteAllTextAsync("output/source-publisher-accounts/native-migration-review.json", JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(summary));
        if (!apply) return;
        var backupPath = $"output/source-publisher-accounts/source-backup-{now:yyyyMMdd-HHmmss}.json";
        await File.WriteAllTextAsync(backupPath, JsonSerializer.Serialize(candidates.Select(row => row.Source), new JsonSerializerOptions { WriteIndented = true }));
        await using var transaction = await db.Database.BeginTransactionAsync();
        await using (var table = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS homeji.source_publisher_listing_links (
              source_listing_id uuid PRIMARY KEY REFERENCES homeji.rental_source_listings(id),
              rental_post_id uuid UNIQUE NOT NULL REFERENCES homeji.rental_posts(id),
              owner_id uuid NOT NULL REFERENCES homeji.user_profiles(id),
              source_snapshot jsonb NOT NULL, created_at timestamptz NOT NULL DEFAULT now());
            ALTER TABLE homeji.source_publisher_listing_links ENABLE ROW LEVEL SECURITY;
            REVOKE ALL ON TABLE homeji.source_publisher_listing_links FROM PUBLIC, anon, authenticated;
            """, connection, (NpgsqlTransaction)transaction.GetDbTransaction())) await table.ExecuteNonQueryAsync();
        db.RentalPosts.AddRange(candidates.Select(row => row.Post));
        await db.SaveChangesAsync();
        foreach (var row in candidates)
        {
            await using var link = new NpgsqlCommand("""
                INSERT INTO homeji.source_publisher_listing_links(source_listing_id,rental_post_id,owner_id,source_snapshot)
                VALUES(@source,@source,@owner,CAST(@snapshot AS jsonb))
                """, connection, (NpgsqlTransaction)transaction.GetDbTransaction());
            link.Parameters.AddWithValue("source", row.Source.Id); link.Parameters.AddWithValue("owner", row.Owner.Id);
            link.Parameters.AddWithValue("snapshot", JsonSerializer.Serialize(row.Source));
            await link.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
        await File.WriteAllTextAsync("output/source-publisher-accounts/public-listing-links.json", JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(new { nativeListingsCreated = candidates.Count(row => row.Post.Status == RentalPostStatus.Active), draftListingsCreated = candidates.Count(row => row.Post.Status == RentalPostStatus.Draft), linkedAccounts = candidates.Select(row => row.Owner.Id).Distinct().Count(), sourceRecordsDeleted = 0 }));
    }
}
