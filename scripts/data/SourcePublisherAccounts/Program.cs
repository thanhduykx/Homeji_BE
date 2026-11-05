using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

// Run from repository root. Preview is read-only; --apply creates pending accounts.
var apply = args.Contains("--apply");
var resolutionIndex = Array.IndexOf(args, "--resolutions");
var remaining = args.Where((_, index) => index != resolutionIndex && index != resolutionIndex + 1).ToArray();
if (resolutionIndex < 0) remaining = args;
if (remaining.Any(a => a != "--apply") || (resolutionIndex >= 0 && resolutionIndex == args.Length - 1))
    throw new ArgumentException("Use [--apply] [--resolutions JSON_PATH].");
var resolutions = resolutionIndex >= 0
    ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(args[resolutionIndex + 1]))!
    : new Dictionary<string, string>();
using var config = JsonDocument.Parse(File.ReadAllText("src/Homeji.Api/appsettings.Local.json"));
using var defaults = JsonDocument.Parse(File.ReadAllText("src/Homeji.Api/appsettings.json"));
var connectionSettings = new NpgsqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
var auth = config.RootElement.GetProperty("Supabase");
var projectUrl = new Uri((auth.TryGetProperty("ProjectUrl", out var localUrl) ? localUrl : defaults.RootElement.GetProperty("Supabase").GetProperty("ProjectUrl")).GetString()!);
var project = projectUrl.Host.Split('.')[0];
if (projectUrl.Scheme != "https" || !projectUrl.Host.EndsWith(".supabase.co", StringComparison.Ordinal)
    || connectionSettings.Database != "postgres" || connectionSettings.Username != "postgres." + project)
    throw new InvalidOperationException("Database and authentication project must match.");
await using var db = new NpgsqlConnection(connectionSettings.ConnectionString);
await db.OpenAsync();
// Prevent two operators creating the same account concurrently.
await using var mutex = new NpgsqlCommand("SELECT pg_try_advisory_lock(683010512)", db);
if (!(bool)(await mutex.ExecuteScalarAsync())!) throw new InvalidOperationException("Another import is running.");
var rows = new List<(Guid Id, string Name, string Phone)>();
await using (var command = new NpgsqlCommand("SELECT id, publisher_name, contact_phone FROM homeji.rental_source_listings WHERE NULLIF(trim(publisher_name),'') IS NOT NULL AND contact_phone IS NOT NULL ORDER BY id", db))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) rows.Add((reader.GetGuid(0), reader.GetString(1).Trim(), reader.GetString(2).Trim()));
var eligible = rows.Where(r => Regex.IsMatch(r.Phone, "^0[35789][0-9]{8}$") && r.Name.Length <= 100 && !r.Name.Any(char.IsControl)).GroupBy(r => r.Phone).ToArray();
using var http = new HttpClient { BaseAddress = projectUrl, Timeout = TimeSpan.FromSeconds(30) };
var key = auth.TryGetProperty("ServiceRoleKey", out var keyValue) ? keyValue.GetString() : null;
if (apply && string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Missing service role key.");
http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
http.DefaultRequestHeaders.Add("apikey", key ?? "");
var created = 0; var existing = 0; var conflicting = 0; var repaired = 0; var pendingChecked = 0; var renamedListings = 0;
var conflicts = new List<object>();
foreach (var group in eligible)
{
    var names = group.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    var resolved = resolutions.TryGetValue(group.Key, out var approvedName);
    if (resolved && !names.Contains(approvedName, StringComparer.Ordinal))
        throw new InvalidOperationException("Approved name is absent from current source data.");
    if (names.Length != 1 && !resolved) { conflicting++; conflicts.Add(new { phone = group.Key, names, listingIds = group.Select(r => r.Id).ToArray() }); continue; }
    var displayName = resolved ? approvedName! : names[0];
    var phone = "84" + group.Key[1..];
    Guid? userId = null; bool imported = false;
    await using (var lookup = new NpgsqlCommand("SELECT id, COALESCE(raw_app_meta_data->>'source_publisher_import','') = 'true' FROM auth.users WHERE phone IN (@phone, @local, @plus) AND deleted_at IS NULL", db))
    {
        lookup.Parameters.AddWithValue("phone", phone); lookup.Parameters.AddWithValue("local", group.Key); lookup.Parameters.AddWithValue("plus", "+" + phone);
        await using var reader = await lookup.ExecuteReaderAsync();
        if (await reader.ReadAsync()) { userId = reader.GetGuid(0); imported = reader.GetBoolean(1); }
        if (await reader.ReadAsync()) { conflicting++; continue; }
    }
    if (userId.HasValue && !imported) { existing++; continue; }
    if (!apply && !userId.HasValue) continue;
    if (!userId.HasValue)
    {
        // No email, no notification, random undisclosed password, neither contact verified.
        using var response = await http.PostAsJsonAsync("auth/v1/admin/users", new {
            phone, password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), phone_confirm = false, email_confirm = false,
            ban_duration = "876000h", user_metadata = new { full_name = displayName },
            app_metadata = new { source_publisher_import = true, account_status = "pending_phone_verification", source_listing_ids = group.Select(r => r.Id).ToArray() }
        });
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Account creation failed (HTTP {(int)response.StatusCode}); no response body logged.");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        userId = body.RootElement.GetProperty("id").GetGuid();
        created++;
    }
    // Verify provider state before exposing any app profile. Existing claimed accounts are never changed.
    await using (var verify = new NpgsqlCommand("SELECT count(*) FROM auth.users WHERE id=@id AND email_confirmed_at IS NULL AND phone_confirmed_at IS NULL AND banned_until > now() AND (NULLIF(email,'') IS NULL OR email LIKE '%@publishers.homeji.invalid') AND raw_app_meta_data->>'account_status'='pending_phone_verification'", db))
    {
        verify.Parameters.AddWithValue("id", userId.Value);
        if ((long)(await verify.ExecuteScalarAsync())! != 1) throw new InvalidOperationException("Pending account verification failed.");
    }
    pendingChecked++;
    if (!apply) continue;
    await using var transaction = await db.BeginTransactionAsync();
    await using var profile = new NpgsqlCommand("""
        INSERT INTO homeji.user_profiles(id,display_name,phone,role,created_at,updated_at)
        VALUES (@id,@name,@phone,2,now(),now()) ON CONFLICT(id) DO NOTHING
        """, db, transaction);
    profile.Parameters.AddWithValue("id", userId.Value); profile.Parameters.AddWithValue("name", displayName); profile.Parameters.AddWithValue("phone", group.Key);
    repaired += await profile.ExecuteNonQueryAsync();
    if (resolved)
    {
        await using var rename = new NpgsqlCommand("UPDATE homeji.rental_source_listings SET publisher_name=@name WHERE id=ANY(@ids) AND contact_phone=@phone AND publisher_name IS DISTINCT FROM @name", db, transaction);
        rename.Parameters.AddWithValue("name", displayName); rename.Parameters.AddWithValue("phone", group.Key);
        rename.Parameters.AddWithValue("ids", group.Select(r => r.Id).ToArray());
        renamedListings += await rename.ExecuteNonQueryAsync();
    }
    await transaction.CommitAsync();
}
var summary = new { apply, namedPhoneListings = rows.Count, validPhoneGroups = eligible.Length, conflictingNameGroups = conflicting, existingAccountsUntouched = existing, created, profilesCreated = repaired, pendingAccountsVerified = pendingChecked, renamedListings };
var output = Path.Combine("output", "source-publisher-accounts");
Directory.CreateDirectory(output);
await File.WriteAllTextAsync(Path.Combine(output, "latest-review.json"), JsonSerializer.Serialize(new { summary, conflicts }, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(summary));
