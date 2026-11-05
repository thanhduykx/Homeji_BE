using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;

if (args.Contains("--self-test"))
{
    var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");
    if (Email("Dương Thành Thanh Duy", id) != "duong.thanh.thanh.duy.12345678123412341234123456789abc@publishers.homeji.invalid"
        || Email("Nguyễn An", id) == Email("Nguyễn An", Guid.NewGuid())
        || Email(new string('a', 200), id).Split('@')[0].Length > 64
        || Email("☎", id).Split('@')[0] != "nguoi.dang." + id.ToString("N"))
        throw new InvalidOperationException("Email normalization test failed.");
    Console.WriteLine("PASS Vietnamese name normalization, stable IDs, same-name uniqueness, length limits, fallback.");
    return;
}
var apply = args.Contains("--apply");
var publish = args.Contains("--publish");
var verifyPublic = args.Contains("--verify");
if (args.Any(value => value is not ("--apply" or "--publish" or "--verify")) || verifyPublic && apply) throw new ArgumentException("Use [--apply] [--publish], --verify, or --self-test.");
using var config = JsonDocument.Parse(File.ReadAllText("src/Homeji.Api/appsettings.Local.json"));
using var defaults = JsonDocument.Parse(File.ReadAllText("src/Homeji.Api/appsettings.json"));
var settings = new NpgsqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString());
var auth = config.RootElement.GetProperty("Supabase");
var url = new Uri((auth.TryGetProperty("ProjectUrl", out var localUrl) ? localUrl : defaults.RootElement.GetProperty("Supabase").GetProperty("ProjectUrl")).GetString()!);
if (url.Scheme != "https" || !url.Host.EndsWith(".supabase.co", StringComparison.Ordinal) || settings.Database != "postgres" || settings.Username != "postgres." + url.Host.Split('.')[0])
    throw new InvalidOperationException("Auth and database projects must match.");
await using var db = new NpgsqlConnection(settings.ConnectionString);
await db.OpenAsync();
await using var mutex = new NpgsqlCommand("SELECT pg_try_advisory_lock(683010512)", db);
if (!(bool)(await mutex.ExecuteScalarAsync())!) throw new InvalidOperationException("Another publisher import is running.");
var accounts = new List<(Guid Id, string Name, string? Email)>();
await using (var command = new NpgsqlCommand("""
    SELECT u.id,p.display_name,NULLIF(u.email,'') FROM auth.users u
    JOIN homeji.user_profiles p ON p.id=u.id
    WHERE u.deleted_at IS NULL AND u.raw_app_meta_data->>'source_publisher_import'='true'
      AND u.raw_app_meta_data->>'account_status'='pending_phone_verification'
      AND u.banned_until>now() AND u.phone_confirmed_at IS NULL AND u.email_confirmed_at IS NULL
    ORDER BY u.id
    """, db))
await using (var reader = await command.ExecuteReaderAsync())
    while (await reader.ReadAsync()) accounts.Add((reader.GetGuid(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
using var http = new HttpClient { BaseAddress = url, Timeout = TimeSpan.FromSeconds(30) };
if (apply)
{
    var key = auth.GetProperty("ServiceRoleKey").GetString();
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
    http.DefaultRequestHeaders.Add("apikey", key);
}
var updated = 0;
foreach (var account in accounts)
{
    var email = Email(account.Name, account.Id);
    if (account.Email != null && !account.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("An imported account already has a different email. No replacement performed.");
    await using var duplicate = new NpgsqlCommand("SELECT count(*) FROM auth.users WHERE lower(email)=lower(@email) AND id<>@id", db);
    duplicate.Parameters.AddWithValue("email", email); duplicate.Parameters.AddWithValue("id", account.Id);
    if ((long)(await duplicate.ExecuteScalarAsync())! > 0) throw new InvalidOperationException("Generated email collision; stopped.");
    if (account.Email != null) continue;
    updated++;
    if (!apply) continue;
    using var response = await http.PutAsJsonAsync("auth/v1/admin/users/" + account.Id, new { email, email_confirm = false });
    if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"Admin email update failed (HTTP {(int)response.StatusCode}); response body omitted.");
    await using var verify = new NpgsqlCommand("SELECT count(*) FROM auth.users WHERE id=@id AND email=@email AND email_confirmed_at IS NULL AND phone_confirmed_at IS NULL AND banned_until>now()", db);
    verify.Parameters.AddWithValue("id", account.Id); verify.Parameters.AddWithValue("email", email);
    if ((long)(await verify.ExecuteScalarAsync())! != 1) throw new InvalidOperationException("Account update readback failed.");
}
await using var audit = new NpgsqlCommand("""
    SELECT count(*),count(DISTINCT p.id),count(*) FILTER(WHERE s.latitude IS NULL OR s.longitude IS NULL),
      count(*) FILTER(WHERE EXISTS(SELECT 1 FROM homeji.rental_posts n WHERE n.id=s.id)),
      count(*) FILTER(WHERE jsonb_array_length(s.image_urls)=0)
    FROM homeji.rental_source_listings s JOIN homeji.user_profiles p ON p.phone=s.contact_phone
    JOIN auth.users u ON u.id=p.id AND u.raw_app_meta_data->>'source_publisher_import'='true'
    """, db);
await using var auditReader = await audit.ExecuteReaderAsync();
await auditReader.ReadAsync();
var summary = new { apply, importedAccounts = accounts.Count, emailsChangedOrPlanned = updated,
    sourceListingsMatched = auditReader.GetInt64(0), publisherProfilesMatched = auditReader.GetInt64(1),
    listingsWithoutCoordinates = auditReader.GetInt64(2), alreadyNativeListings = auditReader.GetInt64(3),
    listingsWithoutImages = auditReader.GetInt64(4) };
Directory.CreateDirectory("output/source-publisher-accounts");
await File.WriteAllTextAsync("output/source-publisher-accounts/email-completion.json", JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine(JsonSerializer.Serialize(summary));
await auditReader.DisposeAsync();
if (publish) await PublishSourceListings.RunAsync(db, accounts.Select(account => account.Id).ToArray(), apply);
if (verifyPublic) await VerifySourcePublishers.RunAsync(db);

static string Email(string name, Guid id)
{
    var normalized = name.Replace('đ','d').Replace('Đ','D').Normalize(NormalizationForm.FormD);
    var ascii = new string(normalized.Where(value => CharUnicodeInfo.GetUnicodeCategory(value) != UnicodeCategory.NonSpacingMark).ToArray()).ToLowerInvariant();
    var slug = Regex.Replace(ascii, "[^a-z0-9]+", ".").Trim('.');
    if (slug.Length == 0) slug = "nguoi.dang";
    if (slug.Length > 30) slug = slug[..30].Trim('.');
    return $"{slug}.{id:N}@publishers.homeji.invalid";
}
