using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Homeji.Api.IntegrationTests;

public sealed class SearchRateLimitTests
{
    [Fact]
    public async Task Public_search_quota_returns_429_problem_details_and_retry_after()
    {
        using var factory = new HomejiApiFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:PublicSearch:PermitLimit", "2"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false,
        });
        // A missing longitude fails validation before any database I/O.
        var path = new Uri("/api/marketplace-posts?latitude=10.85", UriKind.Relative);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(path)).StatusCode);
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.RetryAfter!.Delta > TimeSpan.Zero);
    }
}
