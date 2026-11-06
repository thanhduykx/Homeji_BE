using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Chatbot;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class PublicCacheAndRateLimitTests
{
    private const string Search = "/api/rental-source-listings?page=1&pageSize=10";
    private const string Secret = "fixture-proxy-secret";

    [Fact]
    public async Task Anonymous_repeat_reads_come_from_the_cache_and_vary_by_query()
    {
        var service = new CountingSources();
        using var factory = Factory(service);
        using var client = Client(factory);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri(Search, UriKind.Relative))).StatusCode);
        using var second = await client.GetAsync(new Uri(Search, UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.NotNull(second.Headers.Age);
        Assert.Equal(1, service.Searches);

        await client.GetAsync(new Uri("/api/rental-source-listings?page=2&pageSize=10", UriKind.Relative));
        Assert.Equal(2, service.Searches);
    }

    [Fact]
    public async Task Signed_in_requests_never_read_or_fill_the_cache()
    {
        var service = new CountingSources();
        using var factory = Factory(service);
        using var client = Client(factory);
        client.DefaultRequestHeaders.Add(TestAuthentication.Header, "1");

        await client.GetAsync(new Uri(Search, UriKind.Relative));
        using var second = await client.GetAsync(new Uri(Search, UriKind.Relative));

        Assert.Equal(2, service.Searches);
        Assert.Null(second.Headers.Age);
    }

    [Fact]
    public async Task Failed_reads_are_not_cached()
    {
        var service = new CountingSources();
        using var factory = Factory(service);
        using var client = Client(factory);
        const string invalid = "/api/rental-source-listings?pageSize=999";
        service.Reject = true;

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(new Uri(invalid, UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(new Uri(invalid, UriKind.Relative))).StatusCode);
        Assert.Equal(2, service.Searches);
    }

    [Fact]
    public async Task Cached_answers_do_not_use_up_the_quota()
    {
        using var factory = Factory(new CountingSources(), permitLimit: 1);
        using var client = Client(factory);

        for (var i = 0; i < 5; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(new Uri(Search, UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Visitors_named_by_the_proxy_get_separate_quotas()
    {
        using var factory = Factory(new CountingSources(), permitLimit: 1, secret: Secret);
        using var client = Client(factory);

        Assert.Equal(HttpStatusCode.OK, await Get(client, 1, Secret, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, 2, Secret, "203.0.113.1"));
        Assert.Equal(HttpStatusCode.OK, await Get(client, 3, Secret, "203.0.113.2"));
    }

    [Theory]
    [InlineData("wrong-secret", "203.0.113.9")]
    [InlineData("", "203.0.113.9")]
    [InlineData(Secret, "not-an-address")]
    public async Task Unverified_identity_cannot_buy_a_new_quota(string secret, string address)
    {
        using var factory = Factory(new CountingSources(), permitLimit: 1, secret: Secret);
        using var client = Client(factory);

        Assert.Equal(HttpStatusCode.OK, await Get(client, 1, secret, address));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, 2, secret, address + "x"));
    }

    [Fact]
    public async Task Without_a_configured_secret_the_headers_are_ignored()
    {
        using var factory = Factory(new CountingSources(), permitLimit: 1);
        using var client = Client(factory);

        Assert.Equal(HttpStatusCode.OK, await Get(client, 1, "", "203.0.113.1"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Get(client, 2, "", "203.0.113.2"));
    }

    private static async Task<HttpStatusCode> Get(HttpClient client, int page, string secret, string address)
    {
        // A distinct page in each call, so the limiter is reached instead of the cache.
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri($"/api/rental-source-listings?page={page}&pageSize=10", UriKind.Relative));
        request.Headers.Add("X-Homeji-Proxy-Secret", secret);
        request.Headers.Add("X-Homeji-Client-IP", address);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static WebApplicationFactory<Program> Factory(CountingSources service, int permitLimit = 60, string? secret = null) =>
        new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("RateLimiting:PublicSearch:PermitLimit", permitLimit.ToString(System.Globalization.CultureInfo.InvariantCulture));
            if (secret is not null) builder.UseSetting("RateLimiting:ProxySecret", secret);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IRentalSourceListingService>();
                services.AddSingleton<IRentalSourceListingService>(service);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthentication.SchemeName;
                    options.DefaultChallengeScheme = TestAuthentication.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthentication>(TestAuthentication.SchemeName, _ => { });
            });
        });

    private sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "PublicCacheTest";
        public const string Header = "X-Test-Signed-In";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(Header)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "11111111-1111-4111-8111-111111111111")], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class CountingSources : IRentalSourceListingService
    {
        private int _searches;
        public int Searches => _searches;
        public bool Reject { get; set; }

        public Task<IReadOnlyList<RentalSourceListingDto>> SearchAsync(RentalSourceSearchDto query, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _searches);
            if (Reject) throw new Homeji.Application.Common.Exceptions.RequestValidationException(new Dictionary<string, string[]> { ["query"] = ["Không hợp lệ."] });
            return Task.FromResult<IReadOnlyList<RentalSourceListingDto>>([]);
        }

        public Task<RentalSourceListingDto> GetAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<RentalSourceContactDto> GetContactAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ChatbotSourceMatchDto> MatchAsync(AiParsedSearchCriteriaDto criteria, int limit, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
