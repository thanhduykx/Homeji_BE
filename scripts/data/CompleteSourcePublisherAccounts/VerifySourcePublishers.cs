using Npgsql;
using System.Text.Json;

internal static class VerifySourcePublishers
{
    public static async Task RunAsync(NpgsqlConnection db)
    {
        await using var command = new NpgsqlCommand("""
            SELECT jsonb_build_object(
              'importedAccounts',(SELECT count(*) FROM auth.users WHERE raw_app_meta_data->>'source_publisher_import'='true' AND deleted_at IS NULL),
              'uniqueAliases',(SELECT count(DISTINCT lower(email)) FROM auth.users WHERE raw_app_meta_data->>'source_publisher_import'='true' AND email LIKE '%@publishers.homeji.invalid' AND deleted_at IS NULL),
              'sourceRecords',(SELECT count(*) FROM homeji.rental_source_listings),
              'linkedListings',(SELECT count(*) FROM homeji.source_publisher_listing_links),
              'privateImportSnapshots',(SELECT relrowsecurity FROM pg_class WHERE oid='homeji.source_publisher_listing_links'::regclass)
                AND NOT has_table_privilege('anon','homeji.source_publisher_listing_links','SELECT')
                AND NOT has_table_privilege('authenticated','homeji.source_publisher_listing_links','SELECT'),
              'activeNativeListings',(SELECT count(*) FROM homeji.source_publisher_listing_links l JOIN homeji.rental_posts p ON p.id=l.rental_post_id WHERE p.status=3),
              'incompleteDrafts',(SELECT count(*) FROM homeji.source_publisher_listing_links l JOIN homeji.rental_posts p ON p.id=l.rental_post_id WHERE p.status=1),
              'wrongOwners',(SELECT count(*) FROM homeji.source_publisher_listing_links l JOIN homeji.rental_posts p ON p.id=l.rental_post_id WHERE p.owner_id<>l.owner_id),
              'inventedOwnerVerification',(SELECT count(*) FROM homeji.user_profiles p JOIN auth.users u ON u.id=p.id WHERE u.raw_app_meta_data->>'source_publisher_import'='true' AND p.landlord_verification_status<>0)
            )::text
            """, db);
        var json = (string)(await command.ExecuteScalarAsync())!;
        Console.WriteLine(json);
        await File.WriteAllTextAsync("output/source-publisher-accounts/database-verification.json", json);
        var samples = new List<(Guid PostId, Guid OwnerId)>();
        await using (var sample = new NpgsqlCommand("""
            SELECT p.id,p.owner_id FROM homeji.source_publisher_listing_links l JOIN homeji.rental_posts p ON p.id=l.rental_post_id
            JOIN homeji.rental_source_listings s ON s.id=l.source_listing_id WHERE p.status=3
            ORDER BY (s.publisher_name ILIKE '%Hậu%') DESC,p.id LIMIT 2
            """, db))
        await using (var reader = await sample.ExecuteReaderAsync())
            while (await reader.ReadAsync()) samples.Add((reader.GetGuid(0), reader.GetGuid(1)));
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(45) };
        foreach (var sample in samples)
        {
            using var response = await http.GetAsync($"https://homeji-be.onrender.com/api/rental-posts/{sample.PostId}");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Public detail readback failed: HTTP {(int)response.StatusCode}.");
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = body.RootElement;
            if (root.GetProperty("id").GetGuid() != sample.PostId || root.GetProperty("ownerId").GetGuid() != sample.OwnerId || root.GetProperty("media").GetArrayLength() < 3)
                throw new InvalidOperationException("Public detail ownership/media readback mismatch.");
        }
        Console.WriteLine(JsonSerializer.Serialize(new { publicNativeDetailsVerified = samples.Count, actualOwnerIdsMatch = true }));
    }
}
