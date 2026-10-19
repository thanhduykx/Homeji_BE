using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using Homeji.Api.Controllers;
using Homeji.Application.IServices.Accounts;
using Homeji.Infrastructure.External;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Homeji.Api.IntegrationTests;

public sealed class SessionRefreshTests : IDisposable
{
    private const string Issuer = "https://auth.example.test/auth/v1";
    private readonly ECDsa _ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly Guid _user = Guid.NewGuid();
    private readonly string _session = Guid.NewGuid().ToString();
    private readonly Revocations _revocations = new();
    private readonly Handler _handler = new();

    [Fact]
    public void MvcCanValidateRefreshRecordWithoutPropertyMetadataErrors()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new ActionContext { HttpContext = new DefaultHttpContext { RequestServices = provider } };
        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, "", new RefreshSessionRequest("bad", "short"));
        Assert.False(context.ModelState.IsValid);
        Assert.Contains("SessionToken", context.ModelState.Keys);
    }

    [Fact]
    public async Task ExpiredSignedAnchorCanRotateWithinSameSession()
    {
        var old = Token(_user, _session, expired: true);
        var renewed = Token(_user, _session);
        _handler.Response = HttpResponseMessageWithToken(renewed);
        var result = await Controller().Refresh(new(old, "refresh-secret"), CancellationToken.None);
        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(1, _handler.Calls);
        Assert.Contains("grant_type=refresh_token", _handler.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokedAnchorCannotBeRenewed()
    {
        _revocations.Before = DateTimeOffset.UtcNow;
        var result = await Controller().Refresh(new(Token(_user, _session, true), "refresh-secret"), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact]
    public async Task TamperedAnchorDoesNotReachProvider()
    {
        var result = await Controller().Refresh(new("invalid.jwt.signature", "refresh-secret"), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result);
        Assert.Equal(0, _handler.Calls);
    }

    [Fact]
    public async Task AnotherSessionCannotBeSubstituted()
    {
        _handler.Response = HttpResponseMessageWithToken(Token(_user, Guid.NewGuid().ToString()));
        var result = await Controller().Refresh(new(Token(_user, _session, true), "refresh-secret"), CancellationToken.None);
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 401)]
    [InlineData(HttpStatusCode.TooManyRequests, 503)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public async Task InvalidRefreshAndTemporaryOutageRemainDistinct(HttpStatusCode status, int expected)
    {
        _handler.Response = new HttpResponseMessage(status);
        var result = await Controller().Refresh(new(Token(_user, _session, true), "refresh-secret"), CancellationToken.None);
        Assert.Equal(expected, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
    }

    private SessionRefreshController Controller()
    {
        var key = new ECDsaSecurityKey(_ec) { KeyId = "test-key" };
        var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
        configuration.SigningKeys.Add(key);
        var options = new JwtBearerOptions {
            ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration),
            TokenValidationParameters = new TokenValidationParameters {
                ValidIssuer = Issuer, ValidAudience = "authenticated", ValidateIssuer = true,
                ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256] } };
        return new SessionRefreshController(new Factory(_handler), Options.Create(new SupaBaseAuthOptions {
            ProjectUrl = "https://auth.example.test", ApiKey = "public-test-key" }), new Monitor(options), _revocations) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    }

    private string Token(Guid user, string session, bool expired = false)
    {
        var start = DateTime.UtcNow.AddHours(-2);
        var jwt = new JwtSecurityToken(Issuer, "authenticated", [new Claim("sub", user.ToString()),
            new Claim("session_id", session), new Claim("iat", new DateTimeOffset(start).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64)],
            start, expired ? DateTime.UtcNow.AddMinutes(-10) : DateTime.UtcNow.AddHours(1),
            new SigningCredentials(new ECDsaSecurityKey(_ec) { KeyId = "test-key" }, SecurityAlgorithms.EcdsaSha256));
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    private HttpResponseMessage HttpResponseMessageWithToken(string token) => new(HttpStatusCode.OK) {
        Content = JsonContent.Create(new { access_token = token, refresh_token = "rotated-refresh", expires_in = 3600, user = new { id = _user, email = "test@example.test" } }) };

    public void Dispose() { _ec.Dispose(); _handler.Dispose(); }

    private sealed class Monitor(JwtBearerOptions value) : IOptionsMonitor<JwtBearerOptions>
    {
        public JwtBearerOptions CurrentValue => value;
        public JwtBearerOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<JwtBearerOptions, string?> listener) => null;
    }
    private sealed class Factory(Handler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string Url { get; private set; } = "";
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.ServiceUnavailable);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Calls++; Url = request.RequestUri!.ToString(); return Task.FromResult(Response); }
    }
    private sealed class Revocations : IUserSessionRevocationService
    {
        public DateTimeOffset? Before { get; set; }
        public Task<DateTimeOffset?> GetRevokedBeforeAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(Before);
        public Task<DateTimeOffset> RevokeAsync(Guid userId, DateTimeOffset revokedBefore, CancellationToken cancellationToken = default) => Task.FromResult(revokedBefore);
    }
}
