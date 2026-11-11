using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using System.Security.Cryptography;
using System.Text.Json;

const string batch = "PUBLISHER_PREMIUM_DEMO_20261105";
const int count = 5, days = 30;
if (args.Contains("--self-test"))
{
    var now = DateTimeOffset.UtcNow;
    var subscription = UserSubscription.CreatePremium(Guid.NewGuid(), batch, "Premium người đăng", null, now, days, now);
    if (!subscription.IsActivePremium(now) || subscription.IsActivePremium(now.AddDays(days)) || subscription.IsActivePremium(now.AddTicks(-1)) || subscription.PaymentTransactionId.HasValue)
        throw new InvalidOperationException("Premium duration/complimentary grant test failed.");
    Console.WriteLine("PASS 30-day entitlement, strict expiry, no payment transaction.");
    return;
}
var apply = args.Contains("--apply");
if (args.Any(value => value != "--apply")) throw new ArgumentException("Use [--apply] or --self-test.");
using var config = JsonDocument.Parse(File.ReadAllText("src/Homeji.Api/appsettings.Local.json"));
using var defaults = JsonDocument.Parse(File.ReadAllText("src/Homeji.Api/appsettings.json"));
var settings = new NpgsqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
var auth = config.RootElement.GetProperty("Supabase");
var url = new Uri((auth.TryGetProperty("ProjectUrl", out var localUrl) ? localUrl : defaults.RootElement.GetProperty("Supabase").GetProperty("ProjectUrl")).GetString()!);
if (url.Scheme != "https" || !url.Host.EndsWith(".supabase.co", StringComparison.Ordinal) || settings.Database != "postgres" || settings.Username != "postgres." + url.Host.Split('.')[0])
    throw new InvalidOperationException("Database and authentication project mismatch.");
await using var connection = new NpgsqlConnection(settings.ConnectionString);
await connection.OpenAsync();
await using var mutex = new NpgsqlCommand("SELECT pg_try_advisory_lock(683010513)", connection);
if (!(bool)(await mutex.ExecuteScalarAsync())!) throw new InvalidOperationException("Another publisher Premium operation is running.");
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
var nowUtc = DateTimeOffset.UtcNow;
var prior = await db.UserSubscriptions.AsNoTracking().Where(row => row.PackageCode == batch).ToArrayAsync();
Directory.CreateDirectory("output/source-publisher-accounts");
var planPath = "output/source-publisher-accounts/premium-grant-20261105-plan.json";
Grant[] plan;
if (prior.Length > 0)
{
    if (prior.Length != count) throw new InvalidOperationException("Unexpected existing batch size; nothing changed.");
    var ids = prior.Select(row => row.UserId).ToArray();
    var profiles = await db.UserProfiles.AsNoTracking().Where(row => ids.Contains(row.Id)).ToArrayAsync();
    plan = profiles.Select(profile => new Grant(profile.Id, profile.DisplayName)).ToArray();
}
else if (File.Exists(planPath)) plan = JsonSerializer.Deserialize<Grant[]>(await File.ReadAllTextAsync(planPath))!;
else
{
    var candidates = new List<Grant>();
    await using var query = new NpgsqlCommand("""
        SELECT p.id,p.display_name FROM homeji.user_profiles p
        JOIN auth.users u ON u.id=p.id
        WHERE u.deleted_at IS NULL AND u.raw_app_meta_data->>'source_publisher_import'='true'
          AND EXISTS(SELECT 1 FROM homeji.source_publisher_listing_links l JOIN homeji.rental_posts r ON r.id=l.rental_post_id
                     WHERE l.owner_id=p.id AND r.owner_id=p.id AND r.status=3)
          AND NOT EXISTS(SELECT 1 FROM homeji.user_subscriptions s WHERE s.user_id=p.id AND s.tier=2 AND s.status=1 AND s.expires_at>now())
        ORDER BY p.id
        """, connection);
    await using (var reader = await query.ExecuteReaderAsync())
        while (await reader.ReadAsync()) candidates.Add(new Grant(reader.GetGuid(0), reader.GetString(1)));
    if (candidates.Count < count) throw new InvalidOperationException("Not enough eligible imported publishers.");
    for (var index = candidates.Count - 1; index > 0; index--) { var other = RandomNumberGenerator.GetInt32(index + 1); (candidates[index], candidates[other]) = (candidates[other], candidates[index]); }
    plan = candidates.Take(count).ToArray();
    await File.WriteAllTextAsync(planPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
}
if (plan.Length != count || plan.Select(row => row.UserId).Distinct().Count() != count) throw new InvalidOperationException("Invalid Premium grant plan.");
var selected = plan.Select(row => row.UserId).ToArray();
if (apply && prior.Length == 0)
{
    await using var transaction = await db.Database.BeginTransactionAsync();
    // Check eligibility again inside the transaction; never replace paid or future entitlements.
    await using var validate = new NpgsqlCommand("""
        SELECT count(*) FROM homeji.user_profiles p JOIN auth.users u ON u.id=p.id
        WHERE p.id=ANY(@ids) AND u.deleted_at IS NULL AND u.raw_app_meta_data->>'source_publisher_import'='true'
          AND EXISTS(SELECT 1 FROM homeji.rental_posts r WHERE r.owner_id=p.id AND r.status=3)
          AND NOT EXISTS(SELECT 1 FROM homeji.user_subscriptions s WHERE s.user_id=p.id AND s.tier=2 AND s.status=1 AND s.expires_at>now())
        """, connection, (NpgsqlTransaction)transaction.GetDbTransaction());
    validate.Parameters.AddWithValue("ids", selected);
    if ((long)(await validate.ExecuteScalarAsync())! != count) throw new InvalidOperationException("Eligibility changed; no grants applied.");
    var grants = selected.Select(id => UserSubscription.CreatePremium(id, batch, "Premium người đăng - cấp quản trị", null, nowUtc, days, nowUtc)).ToArray();
    db.UserSubscriptions.AddRange(grants);
    await db.SaveChangesAsync();
    await transaction.CommitAsync();
}
var activePosts = await db.RentalPosts.AsNoTracking().Where(row => selected.Contains(row.OwnerId) && row.Status == RentalPostStatus.Active).Select(row => new { row.Id, row.OwnerId, row.Title }).ToArrayAsync();
var subscriptions = await db.UserSubscriptions.AsNoTracking().Where(row => selected.Contains(row.UserId) && row.PackageCode == batch).ToArrayAsync();
if (apply && (subscriptions.Length != count || subscriptions.Any(row => !row.IsActivePremium(nowUtc) || row.PaymentTransactionId.HasValue)))
    throw new InvalidOperationException("Premium grant readback failed.");
var report = new { apply, batch, users = plan, durationDays = days, expiresAt = subscriptions.FirstOrDefault()?.ExpiresAt,
    activePosts = activePosts.Length, posts = activePosts, existingBatchReused = prior.Length > 0,
    paymentsCreated = 0, verificationChanged = false };
await File.WriteAllTextAsync("output/source-publisher-accounts/premium-grant-20261105-report.json", JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(new { apply, users = plan.Select(row => row.Name), durationDays = days,
    expiresAt = subscriptions.FirstOrDefault()?.ExpiresAt, activePosts = activePosts.Length, existingBatchReused = prior.Length > 0 }));

internal sealed record Grant(Guid UserId, string Name);
