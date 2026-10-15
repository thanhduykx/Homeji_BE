using System.Text.Json;
using Npgsql;

var root = Directory.GetCurrentDirectory();
var config = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src/Homeji.Api/appsettings.Local.json")));
var settings = new NpgsqlConnectionStringBuilder(config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString()) { Timeout = 20, CommandTimeout = 60 };
if (settings.Host != "aws-1-ap-south-1.pooler.supabase.com" || settings.Database != "postgres" || settings.Username != "postgres.pzxxzcrgpfsbdkyrdvng") throw new InvalidOperationException("Unexpected database target");
var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
var directory = Path.Combine(root, "output/data-import/rental-repair-" + stamp);
Directory.CreateDirectory(directory);
try {
    await using var connection = new NpgsqlConnection(settings.ConnectionString);
    await connection.OpenAsync();
    await using var tx = await connection.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
    async Task<string> Scalar(string sql) {
        await using var cmd = new NpgsqlCommand(sql, connection, tx);
        return (await cmd.ExecuteScalarAsync())?.ToString() ?? "null";
    }
    foreach (var table in new[] {"rental_posts", "rental_post_media", "rental_post_amenities", "rental_source_listings"}) {
        var json = await Scalar($"SELECT COALESCE(jsonb_agg(to_jsonb(t)), '[]'::jsonb)::text FROM homeji.{table} t");
        File.WriteAllText(Path.Combine(directory, table + ".json"), json);
    }
    Console.WriteLine("Snapshot: " + directory);
    if (args.Length == 2 && args[0] == "--apply") {
        await using var command = new NpgsqlCommand(File.ReadAllText(args[1]), connection, tx);
        await command.ExecuteNonQueryAsync();
        File.Copy(args[1], Path.Combine(directory, "applied.sql"));
    } else if (args.Length != 0) throw new InvalidOperationException("Use no arguments or --apply SQL_PATH");
    var summary = await Scalar("""
      SELECT jsonb_build_object('rentalTotal', (SELECT count(*) FROM homeji.rental_posts),
        'active', (SELECT count(*) FROM homeji.rental_posts WHERE status=3),
        'activeSynthetic', (SELECT count(*) FROM homeji.rental_posts WHERE status=3 AND is_synthetic),
        'sourceTotal', (SELECT count(*) FROM homeji.rental_source_listings),
        'quan9Sources', (SELECT count(*) FROM homeji.rental_source_listings WHERE district='quan-9'),
        'expiredSources', (SELECT count(*) FROM homeji.rental_source_listings WHERE source_expires_at <= CURRENT_TIMESTAMP))::text
      """);
    File.WriteAllText(Path.Combine(directory, "summary.json"), summary);
    await tx.CommitAsync();
    Console.WriteLine(summary);
} catch(Exception ex) {
    Console.WriteLine(JsonSerializer.Serialize(new {errorType=ex.GetType().Name, sqlState=(ex as PostgresException)?.SqlState}));
    Environment.ExitCode=1;
}
