using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Api.Views.Profiles;
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

public sealed class ProfileLifestyleRoleTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData(UserRole.Admin, UserRole.Renter, UserRole.Admin, HttpStatusCode.OK)]
    [InlineData(UserRole.Admin, UserRole.Landlord, UserRole.Admin, HttpStatusCode.OK)]
    [InlineData(UserRole.Renter, UserRole.Admin, UserRole.Renter, HttpStatusCode.BadRequest)]
    [InlineData(UserRole.Landlord, UserRole.Admin, UserRole.Landlord, HttpStatusCode.BadRequest)]
    [InlineData(UserRole.Renter, UserRole.Landlord, UserRole.Landlord, HttpStatusCode.OK)]
    public async Task Real_lifestyle_endpoint_preserves_admin_and_rejects_privilege_escalation(
        UserRole existing, UserRole requested, UserRole expected, HttpStatusCode status)
    {
        var profile = UserProfile.Create(TestUserId, "Test profile", DateTimeOffset.UtcNow);
        profile.SetRole(existing, DateTimeOffset.UtcNow);
        var repository = new ProfileRepository(profile);
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserProfileRepository>();
            services.AddSingleton<IUserProfileRepository>(repository);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "ProfileRoleTest";
                options.DefaultChallengeScheme = "ProfileRoleTest";
            }).AddScheme<AuthenticationSchemeOptions, ProfileAuthenticationHandler>("ProfileRoleTest", _ => { });
        }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        using var response = await client.PutAsJsonAsync(new Uri("/api/profile/me/lifestyle", UriKind.Relative),
            new UpdateLifestyleViewModel(requested, SleepHabit.EarlyBird, PetPreference.NoPets,
                SmokingPreference.NonSmoking, 3_000_000, "Linh Trung"));
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(expected, profile.Role);
        Assert.Equal(status == HttpStatusCode.OK ? 1 : 0, repository.SaveCalls);
        if (status == HttpStatusCode.OK)
        {
            var result = await response.Content.ReadFromJsonAsync<UserProfileViewModel>();
            Assert.NotNull(result);
            Assert.Equal(expected, result.Role);
            Assert.Equal("Linh Trung", result.PreferredArea);
            Assert.True(result.OnboardingCompleted);
        }
    }

    private sealed class ProfileRepository(UserProfile profile) : IUserProfileRepository
    {
        public int SaveCalls { get; private set; }
        public Task<UserProfile?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(userId == profile.Id ? profile : null);
        public Task<UserProfile> SaveAsync(UserProfile value, CancellationToken cancellationToken = default)
        { Assert.Same(profile, value); SaveCalls++; return Task.FromResult(value); }
        public Task<UserProfile> UpsertAsync(UserProfile value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Guid>> GetAllUserIdsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserProfile>> GetMatchingRentersAsync(string address, decimal price, Guid excludedUserId, int take, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ProfileAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", TestUserId.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
