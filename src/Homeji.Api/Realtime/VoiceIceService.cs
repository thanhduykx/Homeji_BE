using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Homeji.Application.Common.Exceptions;

namespace Homeji.Api.Realtime;

/// <summary>Resolves relay credentials without exposing provider administration tokens.</summary>
public sealed partial class VoiceIceService(HttpClient client, IConfiguration configuration, TimeProvider timeProvider)
{
    private bool UsesCloudflare => string.Equals(configuration["VoiceCalls:Provider"], "Cloudflare", StringComparison.OrdinalIgnoreCase);
    private bool UsesMetered => string.Equals(configuration["VoiceCalls:Provider"], "Metered", StringComparison.OrdinalIgnoreCase);
    public bool IsConfigured => UsesMetered
        ? MeteredAppName().IsMatch(configuration["VoiceCalls:Metered:AppName"] ?? "") && !string.IsNullOrWhiteSpace(configuration["VoiceCalls:Metered:ApiKey"])
        : UsesCloudflare
        ? Guid.TryParse(configuration["VoiceCalls:Cloudflare:KeyId"], out _) && !string.IsNullOrWhiteSpace(configuration["VoiceCalls:Cloudflare:ApiToken"])
        : string.Equals(configuration["VoiceCalls:Provider"] ?? "Coturn", "Coturn", StringComparison.OrdinalIgnoreCase)
          && !string.IsNullOrWhiteSpace(configuration["VoiceCalls:TurnSecret"])
          && (configuration.GetSection("VoiceCalls:TurnUrls").Get<string[]>() ?? []).Length > 0;

    public async Task<VoiceIceConfiguration> CreateAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) return new(false, []);
        if (userId == Guid.Empty) throw new UnauthorizedAccessException("Bạn cần đăng nhập để gọi thoại.");
        if (!UsesCloudflare && !UsesMetered) return CreateCoturn(userId);

        using var request = BuildRequest();
        try
        {
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            IReadOnlyList<VoiceIceServer>? servers;
            if (UsesMetered)
                servers = ReadMeteredServers(await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken));
            else
                servers = (await response.Content.ReadFromJsonAsync<CloudflareIceResponse>(cancellationToken))?.IceServers;
            if (servers is not { Count: > 0 and <= 10 } ||
                servers.Any(server => server is null || server.Urls is not { Count: > 0 and <= 10 } || server.Urls.Any(url => string.IsNullOrWhiteSpace(url) || !(UsesMetered ? MeteredIceUrl() : CloudflareIceUrl()).IsMatch(url))) ||
                !servers.Any(server => server.Urls.Any(IsRelayUrl) &&
                    !string.IsNullOrWhiteSpace(server.Username) && !string.IsNullOrWhiteSpace(server.Credential)))
                throw Unavailable();
            return new(true, servers);
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw Unavailable(); }
    }

    private HttpRequestMessage BuildRequest()
    {
        if (UsesMetered)
        {
            var appName = configuration["VoiceCalls:Metered:AppName"]!;
            var apiKey = Uri.EscapeDataString(configuration["VoiceCalls:Metered:ApiKey"]!);
            // Credential-scoped key only; never use Metered's account Secret Key here.
            return new(HttpMethod.Get, $"https://{appName}.metered.live/api/v1/turn/credentials?apiKey={apiKey}");
        }
        var keyId = configuration["VoiceCalls:Cloudflare:KeyId"]!;
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://rtc.live.cloudflare.com/v1/turn/keys/{Uri.EscapeDataString(keyId)}/credentials/generate-ice-servers");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", configuration["VoiceCalls:Cloudflare:ApiToken"]);
        request.Content = JsonContent.Create(new { ttl = 7200 });
        return request;
    }

    private static List<VoiceIceServer> ReadMeteredServers(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array) throw Unavailable();
        var servers = new List<VoiceIceServer>();
        foreach (var item in json.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("urls", out var urls)) throw Unavailable();
            string[] values;
            if (urls.ValueKind == JsonValueKind.String) values = [urls.GetString()!];
            else if (urls.ValueKind == JsonValueKind.Array && urls.EnumerateArray().All(url => url.ValueKind == JsonValueKind.String))
                values = urls.EnumerateArray().Select(url => url.GetString()!).ToArray();
            else throw Unavailable();
            var username = item.TryGetProperty("username", out var user) && user.ValueKind == JsonValueKind.String ? user.GetString() : null;
            var credential = item.TryGetProperty("credential", out var password) && password.ValueKind == JsonValueKind.String ? password.GetString() : null;
            servers.Add(new(values, username, credential));
        }
        return servers;
    }

    private VoiceIceConfiguration CreateCoturn(Guid userId)
    {
        var username = $"{timeProvider.GetUtcNow().AddHours(2).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)}:{userId:D}";
        var secret = configuration["VoiceCalls:TurnSecret"]!;
        // coturn TURN REST authentication specifically requires HMAC-SHA1.
#pragma warning disable CA5350
        var credential = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(username)));
#pragma warning restore CA5350
        return new(true, [new(configuration.GetSection("VoiceCalls:TurnUrls").Get<string[]>()!, username, credential)]);
    }

    private static bool IsRelayUrl(string url) => url.StartsWith("turn:", StringComparison.Ordinal) || url.StartsWith("turns:", StringComparison.Ordinal);
    private static ExternalServiceUnavailableException Unavailable() => new("TURN", "Dịch vụ gọi thoại tạm thời không phản hồi. Vui lòng thử lại sau.", TimeSpan.FromSeconds(15));

    [GeneratedRegex(@"^(?:stun:stun|turn:turn|turns:turn)\.cloudflare\.com:(?:80|443|3478|5349)(?:\?transport=(?:udp|tcp))?$", RegexOptions.CultureInvariant)]
    private static partial Regex CloudflareIceUrl();

    [GeneratedRegex(@"^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex MeteredAppName();

    [GeneratedRegex(@"^(?:stun|turn|turns):(?:[a-z0-9-]+\.)*relay\.metered\.(?:ca|live):(?:80|443|3478|5349)(?:\?transport=(?:udp|tcp))?$", RegexOptions.CultureInvariant)]
    private static partial Regex MeteredIceUrl();

    private sealed record CloudflareIceResponse(IReadOnlyList<VoiceIceServer>? IceServers);
}

public sealed record VoiceIceServer(IReadOnlyList<string> Urls, string? Username = null, string? Credential = null);
public sealed record VoiceIceConfiguration(bool Available, IReadOnlyList<VoiceIceServer> IceServers);
