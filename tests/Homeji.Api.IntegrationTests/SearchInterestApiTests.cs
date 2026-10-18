using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.RentalPosts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class SearchInterestApiTests
{
    [Theory]
    [InlineData("observations", true)]
    [InlineData("observations", false)]
    [InlineData("following", true)]
    [InlineData("following", false)]
    public async Task Real_HTTP_model_binding_accepts_valid_record_criteria_and_rejects_invalid_area(string route, bool valid)
    {
        var service = new CapturingService();
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ISearchInterestService>();
            services.AddSingleton<ISearchInterestService>(service);
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "InterestTest";
                options.DefaultChallengeScheme = "InterestTest";
            }).AddScheme<AuthenticationSchemeOptions, InterestAuthenticationHandler>("InterestTest", _ => { });
        }));
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        var criteria = new InterestCriteria(valid ? "Thủ Đức" : "X", MaxPrice: 4000000);
        object body = route == "observations" ? new ObserveInterest(Guid.NewGuid(), criteria) : criteria;
        using var response = await client.PostAsJsonAsync(new Uri("/api/search-interests/" + route, UriKind.Relative), body);
        Assert.Equal(valid ? HttpStatusCode.OK : HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(valid ? 1 : 0, service.Calls);
        if (valid) Assert.Equal(4000000, service.Criteria!.MaxPrice);
    }

    private sealed class InterestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "11111111-1111-4111-8111-111111111111")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    private sealed class CapturingService : ISearchInterestService
    {
        public int Calls { get; private set; }
        public InterestCriteria? Criteria { get; private set; }
        private Task<InterestOverview> Capture(InterestCriteria criteria)
        { Calls++; Criteria = criteria; return Task.FromResult(new InterestOverview(new(true), [], [], [])); }
        public Task<InterestOverview> ObserveAsync(ObserveInterest request, CancellationToken ct) => Capture(request.Criteria);
        public Task<InterestOverview> FollowAsync(InterestCriteria request, CancellationToken ct) => Capture(request);
        public Task<InterestOverview> GetAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<InterestOverview> SettingsAsync(InterestSettings request, CancellationToken ct) => throw new NotSupportedException();
        public Task<InterestOverview> RemoveAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<InterestOverview> DismissAsync(string area, CancellationToken ct) => throw new NotSupportedException();
        public Task<InterestOverview> ClearAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ViewedAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<AiInterestProposal> InferAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<InterestOverview> RankAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task ProcessBatchAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
