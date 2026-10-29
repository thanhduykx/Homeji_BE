using System.Net;
using System.Text.Json;
using Homeji.Api.Realtime;
using Homeji.Application.Common.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Homeji.Api.IntegrationTests;

public sealed class VoiceIceServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Fact]
    public async Task Metered_fetches_only_credential_scoped_key_and_normalizes_string_urls()
    {
        using var handler = new TestHandler((request, _) =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://homeji-test.metered.live/api/v1/turn/credentials?apiKey=credential-scoped-key", request.RequestUri!.ToString());
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(Response("""
                [{"urls":"stun:stun.relay.metered.ca:80"},{"urls":"turn:standard.relay.metered.ca:80?transport=tcp","username":"user","credential":"password"},{"urls":["turns:standard.relay.metered.ca:443?transport=tcp"],"username":"user","credential":"password"}]
                """));
        });
        using var client = new HttpClient(handler);
        var result = await new VoiceIceService(client, MeteredConfiguration("homeji-test"), TimeProvider.System).CreateAsync(UserId);
        Assert.True(result.Available);
        Assert.Equal(3, result.IceServers.Count);
        Assert.Equal("turn:standard.relay.metered.ca:80?transport=tcp", Assert.Single(result.IceServers[1].Urls));
        Assert.DoesNotContain("credential-scoped-key", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../localhost")]
    [InlineData("homeji.example.com")]
    [InlineData("host@127.0.0.1")]
    [InlineData("homeji/redirect")]
    public async Task Metered_app_names_cannot_redirect_requests_to_other_hosts(string appName)
    {
        using var handler = new TestHandler((_, _) => throw new InvalidOperationException("Must not contact provider"));
        using var client = new HttpClient(handler);
        var service = new VoiceIceService(client, MeteredConfiguration(appName), TimeProvider.System);
        Assert.False(service.IsConfigured);
        Assert.False((await service.CreateAsync(UserId)).Available);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"urls\":[null]}]")]
    [InlineData("[{\"urls\":\"turn:evil.example:443\",\"username\":\"u\",\"credential\":\"p\"}]")]
    [InlineData("[{\"urls\":\"stun:stun.relay.metered.ca:80\"}]")]
    public async Task Metered_rejects_malformed_or_non_relay_credential_responses(string payload)
    {
        using var handler = new TestHandler((_, _) => Task.FromResult(Response(payload)));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => new VoiceIceService(client, MeteredConfiguration("homeji-test"), TimeProvider.System).CreateAsync(UserId));
    }

    [Fact]
    public async Task Cloudflare_requests_short_lived_credentials_without_leaking_admin_token()
    {
        using var handler = new TestHandler(async (request, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://rtc.live.cloudflare.com/v1/turn/keys/11111111222233334444555555555555/credentials/generate-ice-servers", request.RequestUri!.ToString());
            Assert.Equal("Bearer private-administration-token", request.Headers.Authorization!.ToString());
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal(7200, json.RootElement.GetProperty("ttl").GetInt32());
            return Response("""
                {"iceServers":[{"urls":["stun:stun.cloudflare.com:3478"]},{"urls":["turn:turn.cloudflare.com:3478?transport=udp","turns:turn.cloudflare.com:443?transport=tcp"],"username":"temporary-user","credential":"temporary-credential"}]}
                """);
        });
        using var client = new HttpClient(handler);
        var service = new VoiceIceService(client, CloudflareConfiguration(), TimeProvider.System);
        Assert.True(service.IsConfigured);
        var result = await service.CreateAsync(UserId);
        Assert.True(result.Available);
        Assert.Equal(2, result.IceServers.Count);
        Assert.DoesNotContain("private-administration-token", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"iceServers\":[]}")]
    [InlineData("{\"iceServers\":[null]}")]
    [InlineData("{\"iceServers\":[{\"urls\":[null]}]}")]
    [InlineData("{\"iceServers\":[{\"urls\":[\"https://evil.example/\"],\"username\":\"u\",\"credential\":\"c\"}]}")]
    [InlineData("{\"iceServers\":[{\"urls\":[\"turn:turn.cloudflare.com:3478?transport=udp\"]}]}")]
    [InlineData("{malformed}")]
    public async Task Invalid_provider_payload_is_a_retryable_unavailable_response(string payload)
    {
        using var handler = new TestHandler((_, _) => Task.FromResult(Response(payload)));
        using var client = new HttpClient(handler);
        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => new VoiceIceService(client, CloudflareConfiguration(), TimeProvider.System).CreateAsync(UserId));
    }

    [Fact]
    public async Task Provider_network_failure_is_unavailable_without_including_secrets()
    {
        using var handler = new TestHandler((_, _) => throw new HttpRequestException("fixture"));
        using var client = new HttpClient(handler);
        var error = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => new VoiceIceService(client, CloudflareConfiguration(), TimeProvider.System).CreateAsync(UserId));
        Assert.Equal("TURN", error.ServiceName);
        Assert.DoesNotContain("private-administration-token", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invalid_or_unknown_provider_configuration_does_not_issue_credentials()
    {
        using var handler = new TestHandler((_, _) => throw new InvalidOperationException("Must not contact provider"));
        using var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["VoiceCalls:Provider"] = "Cloudflare", ["VoiceCalls:Cloudflare:KeyId"] = "../../evil", ["VoiceCalls:Cloudflare:ApiToken"] = "token" }).Build();
        var service = new VoiceIceService(client, configuration, TimeProvider.System);
        Assert.False(service.IsConfigured);
        Assert.False((await service.CreateAsync(UserId)).Available);
    }

    [Fact]
    public async Task Coturn_keeps_existing_configuration_and_does_not_call_external_api()
    {
        using var handler = new TestHandler((_, _) => throw new InvalidOperationException("Must not contact provider"));
        using var client = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["VoiceCalls:TurnSecret"] = "local-secret", ["VoiceCalls:TurnUrls:0"] = "turn:turn.example.test:3478?transport=udp" }).Build();
        var result = await new VoiceIceService(client, configuration, TimeProvider.System).CreateAsync(UserId);
        Assert.True(result.Available);
        var server = Assert.Single(result.IceServers);
        Assert.EndsWith($":{UserId:D}", server.Username, StringComparison.Ordinal);
        var expiry = long.Parse(server.Username!.Split(':')[0], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(expiry, DateTimeOffset.UtcNow.AddMinutes(119).ToUnixTimeSeconds(), DateTimeOffset.UtcNow.AddMinutes(121).ToUnixTimeSeconds());
        Assert.DoesNotContain("local-secret", JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    private static IConfiguration CloudflareConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["VoiceCalls:Provider"] = "Cloudflare", ["VoiceCalls:Cloudflare:KeyId"] = "11111111222233334444555555555555", ["VoiceCalls:Cloudflare:ApiToken"] = "private-administration-token" }).Build();
    private static IConfiguration MeteredConfiguration(string appName) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["VoiceCalls:Provider"] = "Metered", ["VoiceCalls:Metered:AppName"] = appName, ["VoiceCalls:Metered:ApiKey"] = "credential-scoped-key" }).Build();
    private static HttpResponseMessage Response(string json) => new(HttpStatusCode.Created) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    private sealed class TestHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
