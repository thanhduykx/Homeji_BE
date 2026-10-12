using System.Net;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Homeji.Application.IRepositories.Admin;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class AdminAnalyticsPriceBasisTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData(UserRole.Admin, HttpStatusCode.OK)]
    [InlineData(UserRole.Renter, HttpStatusCode.Forbidden)]
    public async Task Real_endpoint_exposes_separate_price_samples_only_to_admin(UserRole role, HttpStatusCode expected)
    {
        var now = DateTimeOffset.UtcNow;
        var profile = UserProfile.Create(UserId, "Analytics test", now);
        profile.SetRole(role, now);
        var profiles = DispatchProxy.Create<IUserProfileRepository, ProfileStub>();
        ((ProfileStub)(object)profiles).Profile = profile;
        var room = new AdminAnalyticsRentalRow(Guid.NewGuid(), RentalPostStatus.Active, "phường Linh Trung", 10.85m, 106.8m,
            3_000_000, 20, 0, 0, now, "room_month");
        var person = room with { Id = Guid.NewGuid(), Price = 500_000, PriceBasis = "person_month" };
        var snapshot = new SnapshotRepository(new(10, [room, person], [], [], [], []));
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserProfileRepository>();
            services.AddSingleton(profiles);
            services.RemoveAll<IAdminAnalyticsRepository>();
            services.AddSingleton<IAdminAnalyticsRepository>(snapshot);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "AnalyticsTest";
                options.DefaultChallengeScheme = "AnalyticsTest";
            }).AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("AnalyticsTest", _ => { });
        }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.GetAsync(new Uri("/api/admin/analytics/product?days=7", UriKind.Relative));
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(role == UserRole.Admin ? 1 : 0, snapshot.Reads);
        if (role != UserRole.Admin) return;
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var kpis = json.RootElement.GetProperty("kpis");
        Assert.Equal(2, kpis.GetProperty("activeListings").GetInt32());
        Assert.Equal(3_000_000, kpis.GetProperty("medianMonthlyPrice").GetDecimal());
        Assert.Equal(150_000, kpis.GetProperty("averagePricePerSquareMeter").GetDecimal());
        AssertSamples(kpis);
        var area = Assert.Single(json.RootElement.GetProperty("areas").EnumerateArray());
        AssertSamples(area);
        Assert.Equal("insufficient_data", area.GetProperty("priceSignal").GetString());
    }

    private static void AssertSamples(JsonElement value)
    {
        Assert.Equal("room_month", value.GetProperty("priceBasis").GetString());
        Assert.Equal(1, value.GetProperty("priceSampleCount").GetInt32());
        Assert.Equal(1, value.GetProperty("excludedPriceCount").GetInt32());
        Assert.Equal(1, value.GetProperty("priceAreaSampleCount").GetInt32());
    }

    private sealed class SnapshotRepository(AdminAnalyticsSource source) : IAdminAnalyticsRepository
    {
        public int Reads { get; private set; }
        public Task<AdminAnalyticsSource> GetSnapshotAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken cancellationToken = default)
        { Reads++; return Task.FromResult(source); }
    }

    public class ProfileStub : DispatchProxy
    {
        public UserProfile Profile { get; set; } = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IUserProfileRepository.GetByIdAsync)
                ? Task.FromResult<UserProfile?>(Profile) : throw new InvalidOperationException("Unexpected profile repository operation.");
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
