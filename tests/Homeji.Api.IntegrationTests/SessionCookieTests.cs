using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Homeji.Api.Authentication;
using Homeji.Api.Controllers;
using Homeji.Application.DTOs.Accounts;
using Homeji.Application.IServices.Accounts;
using Homeji.Infrastructure.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Homeji.Api.IntegrationTests;

public sealed class SessionCookieTests : IDisposable
{
    private const string Issuer = "https://auth.example.test/auth/v1";
    private const string FrontendOrigin = "https://frontend.example.test";
    private readonly ECDsa _ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Guid _user = Guid.NewGuid();
    private readonly string _session = Guid.NewGuid().ToString();
    private readonly SessionRefreshTests.Revocations _revocations = new();
    private readonly SessionRefreshTests.Handler _handler = new();

    [Fact]
    public async Task ReusableCookieTokenIsReturnedWithoutCallingProvider()
    {
        var anchor = Token(_user, _session, expired: true);
        var access = Token(_user, _session);
        var (controller, context) = Controller(anchor, access, "refresh-secret");

        var result = Assert.IsType<OkObjectResult>(await controller.Current(new(), CancellationToken.None));

        Assert.Equal(0, _handler.Calls);
        Assert.Equal(access, Read(result, "accessToken"));
        Assert.DoesNotContain("refresh-secret", JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task ExpiredCookieTokenRotatesAndNeverExposesRefreshToken()
    {
        var anchor = Token(_user, _session, expired: true);
        var renewed = Token(_user, _session);
        _handler.Response = ProviderSession(renewed);
        var (controller, context) = Controller(anchor, Token(_user, _session, expired: true), "refresh-secret");

        var result = Assert.IsType<OkObjectResult>(await controller.Current(new(), CancellationToken.None));

        Assert.Equal(1, _handler.Calls);
        Assert.Equal(renewed, Read(result, "accessToken"));
        Assert.DoesNotContain("rotated-refresh", JsonSerializer.Serialize(result.Value), StringComparison.Ordinal);
        var written = context.Response.Headers.SetCookie.ToArray();
        Assert.Contains(written, cookie => cookie!.StartsWith($"{SessionCookies.RefreshCookie}=rotated-refresh", StringComparison.Ordinal));
        // The anchor stays on the original token so revocation keeps comparing its issue time.
        Assert.Contains(written, cookie => cookie!.StartsWith($"{SessionCookies.AnchorCookie}={anchor}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ForceRotateIgnoresReusableCookieToken()
    {
        _handler.Response = ProviderSession(Token(_user, _session));
        var (controller, _) = Controller(Token(_user, _session, expired: true), Token(_user, _session), "refresh-secret");

        Assert.IsType<OkObjectResult>(await controller.Current(new(ForceRotate: true), CancellationToken.None));

        Assert.Equal(1, _handler.Calls);
    }

    [Fact]
    public async Task RevokedSessionIsRefusedAndCookiesAreCleared()
    {
        _revocations.Before = DateTimeOffset.UtcNow;
        var (controller, context) = Controller(Token(_user, _session, expired: true), Token(_user, _session), "refresh-secret");

        Assert.IsType<UnauthorizedResult>(await controller.Current(new(), CancellationToken.None));

        Assert.Equal(0, _handler.Calls);
        AssertSessionCleared(context);
    }

    [Fact]
    public async Task ProviderRejectionClearsCookiesButOutageKeepsThem()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.BadRequest);
        var (rejected, rejectedContext) = Controller(Token(_user, _session, expired: true), null, "refresh-secret");
        Assert.IsType<UnauthorizedResult>(await rejected.Current(new(), CancellationToken.None));
        AssertSessionCleared(rejectedContext);

        _handler.Response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var (outage, outageContext) = Controller(Token(_user, _session, expired: true), null, "refresh-secret");
        Assert.Equal(503, Assert.IsAssignableFrom<StatusCodeResult>(await outage.Current(new(), CancellationToken.None)).StatusCode);
        Assert.Equal(0, outageContext.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task AnotherSessionFromProviderIsRefused()
    {
        _handler.Response = ProviderSession(Token(_user, Guid.NewGuid().ToString()));
        var (controller, context) = Controller(Token(_user, _session, expired: true), null, "refresh-secret");

        Assert.IsType<UnauthorizedResult>(await controller.Current(new(), CancellationToken.None));

        AssertSessionCleared(context);
    }

    [Fact]
    public async Task SessionWithoutRefreshTokenIsUsableUntilItExpires()
    {
        var access = Token(_user, _session);
        var (usable, _) = Controller(access, access, null);
        Assert.IsType<OkObjectResult>(await usable.Current(new(), CancellationToken.None));

        var expired = Token(_user, _session, expired: true);
        var (ended, context) = Controller(expired, expired, null);
        Assert.IsType<UnauthorizedResult>(await ended.Current(new(), CancellationToken.None));
        AssertSessionCleared(context);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact]
    public async Task AdoptMovesExistingSessionIntoCookiesWithoutCallingProvider()
    {
        var anchor = Token(_user, _session, expired: true);
        var access = Token(_user, _session);
        var (controller, context) = Controller(null, null, null);

        Assert.IsType<NoContentResult>(await controller.Adopt(new(anchor, access, "refresh-secret"), CancellationToken.None));

        Assert.Equal(0, _handler.Calls);
        var written = context.Response.Headers.SetCookie.ToArray();
        Assert.Contains(written, cookie => cookie!.StartsWith($"{SessionCookies.AccessCookie}={access}", StringComparison.Ordinal));
        Assert.Contains(written, cookie => cookie!.StartsWith($"{SessionCookies.RefreshCookie}=refresh-secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdoptRefusesForgedAndRevokedSessions()
    {
        var (forged, _) = Controller(null, null, null);
        Assert.IsType<UnauthorizedResult>(await forged.Adopt(new("invalid.jwt.signature-padding", null, "refresh-secret"), CancellationToken.None));

        _revocations.Before = DateTimeOffset.UtcNow;
        var (revoked, context) = Controller(null, null, null);
        Assert.IsType<UnauthorizedResult>(await revoked.Adopt(new(Token(_user, _session, expired: true), null, "refresh-secret"), CancellationToken.None));
        AssertSessionCleared(context);
    }

    [Fact]
    public async Task AdoptNeverAttachesAnotherSessionsAccessToken()
    {
        var anchor = Token(_user, _session, expired: true);
        var (controller, context) = Controller(null, null, null);

        await controller.Adopt(new(anchor, Token(_user, Guid.NewGuid().ToString()), "refresh-secret"), CancellationToken.None);

        Assert.Contains(context.Response.Headers.SetCookie.ToArray(),
            cookie => cookie!.StartsWith($"{SessionCookies.AccessCookie}={anchor}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SignOutClearsCookiesEvenWhenProviderFails()
    {
        _handler.Response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var (controller, context) = Controller(Token(_user, _session, expired: true), Token(_user, _session), "refresh-secret");

        Assert.IsType<NoContentResult>(await controller.SignOut(CancellationToken.None));

        Assert.Contains("auth/v1/logout?scope=local", _handler.Url, StringComparison.Ordinal);
        AssertSessionCleared(context);
    }

    [Fact]
    public void OversizedTokensAreNotWrittenAsCookiesThatBrowsersWouldDrop()
    {
        var context = new DefaultHttpContext();
        Assert.False(Cookies().TryWrite(context.Response, new string('a', 3801), "access", "refresh"));
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
    }

    [Theory]
    [InlineData(null, FrontendOrigin)]
    [InlineData(SessionCookies.CookieMode, null)]
    [InlineData(SessionCookies.CookieMode, "https://attacker.example.test")]
    [InlineData(SessionCookies.CookieMode, "http://localhost:5173")]
    [InlineData("other", FrontendOrigin)]
    public async Task SessionEndpointsRefuseUntrustedRequests(string? mode, string? origin)
    {
        using var factory = ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        foreach (var message in new[]
        {
            new HttpRequestMessage(HttpMethod.Post, "/api/account/session") { Content = JsonContent.Create(new { }) },
            new HttpRequestMessage(HttpMethod.Post, "/api/account/session/adopt") { Content = JsonContent.Create(new { sessionToken = new string('a', 30) }) },
            new HttpRequestMessage(HttpMethod.Delete, "/api/account/session"),
        })
        {
            using (message)
            {
                if (mode is not null) message.Headers.Add(SessionCookies.ModeHeader, mode);
                if (origin is not null) message.Headers.Add("Origin", origin);
                using var response = await client.SendAsync(message);
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.False(response.Headers.Contains("Set-Cookie"));
            }
        }
    }

    [Fact]
    public async Task GuestWithoutCookiesReceivesUnauthorized()
    {
        using var factory = ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var message = Trusted(HttpMethod.Post, "/api/account/session", new { });
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CookieModeLoginSetsHardenedCookiesAndHidesRefreshToken()
    {
        using var factory = ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        using var message = Trusted(HttpMethod.Post, "/api/account/login", new { email = "a@example.test", password = "secret-password" });
        using var response = await client.SendAsync(message);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("access-token", body.GetProperty("accessToken").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("refreshToken").ValueKind);

        var cookies = response.Headers.GetValues("Set-Cookie").ToArray();
        foreach (var name in new[] { SessionCookies.RefreshCookie, SessionCookies.AnchorCookie, SessionCookies.AccessCookie })
        {
            var cookie = Assert.Single(cookies, value => value.StartsWith(name + "=", StringComparison.Ordinal));
            Assert.Contains("path=/api/account/session", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        }

        var hint = Assert.Single(cookies, value => value.StartsWith(SessionCookies.HintCookie + "=", StringComparison.Ordinal));
        Assert.DoesNotContain("httponly", hint, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginWithoutCookieModeNeverReturnsRefreshToken()
    {
        using var factory = ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });
        using var response = await client.PostAsJsonAsync("/api/account/login", new { email = "a@example.test", password = "secret-password" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("access-token", body.GetProperty("accessToken").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("refreshToken").ValueKind);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public void LegacyRefreshRouteIsGoneWhileAdoptRemains()
    {
        using var factory = ApiFactory();
        // Read the route table: the authenticated fallback policy answers 401 to anonymous calls on unknown routes.
        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .ToArray();

        Assert.DoesNotContain("api/account/refresh", routes, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("api/account/session/adopt", routes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CrossOriginCookieModeLoginIsRefused()
    {
        using var factory = ApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/account/login")
        {
            Content = JsonContent.Create(new { email = "a@example.test", password = "secret-password" }),
        };
        message.Headers.Add(SessionCookies.ModeHeader, SessionCookies.CookieMode);
        message.Headers.Add("Origin", "https://attacker.example.test");
        using var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    private static HttpRequestMessage Trusted(HttpMethod method, string path, object body)
    {
        var message = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };
        message.Headers.Add(SessionCookies.ModeHeader, SessionCookies.CookieMode);
        message.Headers.Add("Origin", FrontendOrigin);
        return message;
    }

    private static WebApplicationFactory<Program> ApiFactory() =>
        new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BackgroundJobs:Enabled", "false");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("AuthCookies:AllowedOrigins:0", FrontendOrigin);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IAccountService>();
                services.AddSingleton(DispatchProxy.Create<IAccountService, LoginOnlyAccountService>());
            });
        });

    private static SessionCookies Cookies() =>
        new(Options.Create(new AuthCookieOptions { AllowedOrigins = [FrontendOrigin], LifetimeDays = 90 }));

    private static void AssertSessionCleared(HttpContext context)
    {
        var written = context.Response.Headers.SetCookie.ToArray();
        foreach (var name in new[] { SessionCookies.RefreshCookie, SessionCookies.AnchorCookie, SessionCookies.AccessCookie, SessionCookies.HintCookie })
            Assert.Contains(written, cookie => cookie!.StartsWith(name + "=;", StringComparison.Ordinal)
                && cookie.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        // Browsers ignore a __Secure- deletion that lacks the Secure attribute.
        Assert.All(written, cookie => Assert.Contains("secure", cookie!, StringComparison.OrdinalIgnoreCase));
    }

    private (SessionCookieController Controller, DefaultHttpContext Context) Controller(string? anchor, string? access, string? refresh)
    {
        var key = new ECDsaSecurityKey(_ec) { KeyId = "test-key" };
        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(key);
        var options = new JwtBearerOptions
        {
            ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration),
            TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = "authenticated",
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            },
        };
        var rotator = new SessionRotator(
            new SessionRefreshTests.Factory(_handler),
            Options.Create(new SupaBaseAuthOptions { ProjectUrl = "https://auth.example.test", ApiKey = "public-test-key" }),
            new SessionRefreshTests.Monitor(options),
            _revocations);
        var context = new DefaultHttpContext();
        var cookies = new List<string>();
        if (anchor is not null) cookies.Add($"{SessionCookies.AnchorCookie}={anchor}");
        if (access is not null) cookies.Add($"{SessionCookies.AccessCookie}={access}");
        if (refresh is not null) cookies.Add($"{SessionCookies.RefreshCookie}={refresh}");
        if (cookies.Count > 0) context.Request.Headers.Cookie = string.Join("; ", cookies);
        return (new SessionCookieController(rotator, Cookies(), NullLogger<SessionCookieController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        }, context);
    }

    private static string? Read(OkObjectResult result, string property) =>
        JsonSerializer.SerializeToElement(result.Value).GetProperty(property).GetString();

    private string Token(Guid user, string session, bool expired = false)
    {
        var start = DateTime.UtcNow.AddHours(-2);
        var jwt = new JwtSecurityToken(Issuer, "authenticated",
            [
                new Claim("sub", user.ToString()),
                new Claim("session_id", session),
                new Claim("iat", new DateTimeOffset(start).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            ],
            start,
            expired ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddHours(1),
            new SigningCredentials(new ECDsaSecurityKey(_ec) { KeyId = "test-key" }, SecurityAlgorithms.EcdsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private HttpResponseMessage ProviderSession(string token) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new
        {
            access_token = token,
            refresh_token = "rotated-refresh",
            expires_in = 3600,
            user = new { id = _user, email = "test@example.test" },
        }),
    };

    public void Dispose()
    {
        _ec.Dispose();
        _handler.Dispose();
    }

    public class LoginOnlyAccountService : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IAccountService.LoginAsync)
                ? Task.FromResult(new AuthSessionDto("access-token", "bearer", 3600, "refresh-token", Guid.NewGuid(), "a@example.test", false, "ok"))
                : throw new NotSupportedException(targetMethod?.Name);
    }
}
