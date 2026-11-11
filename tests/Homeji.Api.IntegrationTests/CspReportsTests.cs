using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Homeji.Api.Security;
using Homeji.Application.Abstractions.Security;
using Homeji.Application.DTOs.Activities;
using Homeji.Application.IServices.Activities;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class CspReportsTests
{
    private const string Secret = "reset-secret-value";
    private static readonly Uri Endpoint = new("/api/csp-reports", UriKind.Relative);

    [Theory]
    [InlineData("https://user:pw@homeji.onrender.com/reset-password?token=abc#abc", "https://homeji.onrender.com/reset-password")]
    [InlineData("inline", "inline")]
    [InlineData("wasm-eval", "wasm-eval")]
    [InlineData("data:text/html,secret", "data")]
    [InlineData("not a url", "invalid")]
    public void Logged_urls_keep_only_their_location(string raw, string expected)
    {
        Assert.Equal(expected, CspReportParser.SanitizeUrl(raw));
    }

    [Fact]
    public void Reads_the_Chromium_and_WebKit_formats_alike()
    {
        var expected = new CspViolation("https://h.test/a", "script-src-elem", "https://evil.test/x.js", null, 3, "report");

        Assert.Equal(expected, Parse("""{"csp-report":{"document-uri":"https://h.test/a?q=1","effective-directive":"script-src-elem","blocked-uri":"https://evil.test/x.js","line-number":3,"disposition":"report"}}"""));
        Assert.Equal(expected, Parse("""{"type":"csp-violation","url":"https://h.test/a","body":{"documentURL":"https://h.test/a","effectiveDirective":"script-src-elem","blockedURL":"https://evil.test/x.js","lineNumber":3,"disposition":"report"}}"""));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"csp-report":"x"}""")]
    [InlineData("""{"csp-report":[]}""")]
    [InlineData("""{"type":"deprecation","body":{}}""")]
    [InlineData("""{"type":"csp-violation","body":"x"}""")]
    public void Ignores_payloads_that_are_not_violation_reports(string json)
    {
        Assert.Null(Parse(json));
    }

    [Fact]
    public void Directive_values_cannot_forge_extra_log_lines()
    {
        var directive = "a\n{\"event\":\"forged\"}" + (char)0x2028;
        var violation = Parse(JsonSerializer.Serialize(new Dictionary<string, object> { ["csp-report"] = new Dictionary<string, string> { ["effective-directive"] = directive } }));

        Assert.Equal("a{\"event\":\"forged\"}", violation!.Directive);
    }

    [Fact]
    public void Log_volume_is_capped_per_window_and_the_dropped_count_is_reported()
    {
        var logger = new CapturingLogger();
        var clock = new ManualClock();
        var log = new CspReportLog(logger, clock);
        var violation = new CspViolation(null, "x", null, null, null, null);

        for (var i = 0; i < 100; i++) log.Record(violation);
        Assert.Equal(60, logger.Lines.Count);

        clock.Advance(TimeSpan.FromMinutes(1));
        log.Record(violation with { Directive = "y" });

        Assert.Contains("\"event\":\"csp-violations-dropped\",\"count\":40", logger.Lines[60], StringComparison.Ordinal);
        Assert.Contains("\"directive\":\"y\"", logger.Lines[61], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Anonymous_report_is_accepted_and_only_sanitized_fields_are_logged()
    {
        var logger = new CapturingLogger();
        using var factory = Factory(logger);
        using var client = Client(factory);

        using var response = await Post(client, $$$"""{"csp-report":{"document-uri":"https://h.test/a?token={{{Secret}}}","effective-directive":"script-src-elem","blocked-uri":"inline"}}""");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var line = Assert.Single(logger.Lines);
        Assert.Contains("\"event\":\"csp-violation\"", line, StringComparison.Ordinal);
        Assert.Contains("https://h.test/a", line, StringComparison.Ordinal);
        Assert.DoesNotContain(Secret, line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rejects_other_methods_types_and_malformed_or_oversized_bodies()
    {
        var logger = new CapturingLogger();
        using var factory = Factory(logger);
        using var client = Client(factory);

        // A request matching no action falls to the API-wide sign-in requirement, so guests see 401 here.
        using var get = await client.GetAsync(Endpoint);
        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        using var wrongType = await Post(client, """{"csp-report":{"effective-directive":"x"}}""", "text/plain");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongType.StatusCode);
        using var malformed = await Post(client, "{not json");
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        using var oversized = await Post(client, JsonSerializer.Serialize(new { padding = new string('x', 32 * 1024) }));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        Assert.Empty(logger.Lines);
    }

    [Fact]
    public async Task One_client_is_rate_limited()
    {
        using var factory = Factory(new CapturingLogger());
        using var client = Client(factory);

        var last = HttpStatusCode.OK;
        for (var i = 0; i < 61; i++)
        {
            using var response = await Post(client, "{}");
            last = response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);
    }

    [Fact]
    public async Task Reports_sent_with_a_session_cookie_are_not_recorded_as_user_activity()
    {
        var activities = new CountingActivities();
        using var factory = Factory(new CapturingLogger(), activities);
        using var client = Client(factory);
        client.DefaultRequestHeaders.Add(SignedInScheme.Header, "1");

        using var response = await Post(client, """{"csp-report":{"effective-directive":"img-src"}}""");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, activities.Recorded);
    }

    private static CspViolation? Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return CspReportParser.Parse(document.RootElement);
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string body, string contentType = "application/csp-report")
    {
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        return await client.PostAsync(Endpoint, content);
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    private static WebApplicationFactory<Program> Factory(CapturingLogger logger, CountingActivities? activities = null) =>
        new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICspReportLog>();
                services.AddSingleton<ICspReportLog>(new CspReportLog(logger));
                if (activities is null) return;
                services.RemoveAll<IUserActivityService>();
                services.AddSingleton<IUserActivityService>(activities);
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = SignedInScheme.SchemeName;
                    options.DefaultChallengeScheme = SignedInScheme.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, SignedInScheme>(SignedInScheme.SchemeName, _ => { });
            });
        });

    private sealed class ManualClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class CapturingLogger : ILogger<CspReportLog>
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullLogger.Instance.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (Lines) Lines.Add(formatter(state, exception));
        }
    }

    private sealed class CountingActivities : IUserActivityService
    {
        private int _recorded;
        public int Recorded => _recorded;

        public Task RecordAsync(
            Guid userId,
            string action,
            string resourcePath,
            string httpMethod,
            int responseStatusCode,
            UserActivityType type = UserActivityType.General,
            Guid? relatedEntityId = null,
            string? details = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _recorded);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UserActivityDto>> GetMineAsync(UserActivityType? type, int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class SignedInScheme(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "CspReportTest";
        public const string Header = "X-Test-Signed-In";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey(Header)) return Task.FromResult(AuthenticateResult.NoResult());
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "11111111-1111-4111-8111-111111111111")], SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }
}
