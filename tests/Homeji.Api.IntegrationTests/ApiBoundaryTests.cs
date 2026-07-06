using System.Net;

namespace Homeji.Api.IntegrationTests;

public sealed class ApiBoundaryTests : IClassFixture<HomejiApiFactory>
{
    private readonly HttpClient _client;

    public ApiBoundaryTests(HomejiApiFactory factory)
    {
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });
    }

    [Fact]
    public async Task LiveHealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProfileEndpoint_WithoutAccessToken_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync(new Uri("/api/v1/profile/me", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
