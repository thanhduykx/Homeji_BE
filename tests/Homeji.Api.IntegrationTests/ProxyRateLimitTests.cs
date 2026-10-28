using System.Net;
using Homeji.Api.RateLimiting;
using Homeji.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Homeji.Api.IntegrationTests;

public sealed class ProxyRateLimitTests
{
    [Fact]
    public async Task One_client_exhausting_quota_behind_trusted_proxy_does_not_block_another()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.Production.json"))
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:PublicSearch:PermitLimit"] = "1",
            });
        builder.Services.AddTrustedProxyHeaders(builder.Configuration);
        builder.Services.AddHomejiRateLimiting(builder.Configuration);
        await using var app = builder.Build();
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse("10.1.2.3");
            return next(context);
        });
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/search", () => Results.Ok()).RequireRateLimiting(RateLimitingPolicyNames.PublicSearch);
        await app.StartAsync();
        using var client = app.GetTestClient();

        async Task<HttpStatusCode> Search(string forwarded)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/search");
            request.Headers.Add("X-Forwarded-For", forwarded);
            using var response = await client.SendAsync(request);
            return response.StatusCode;
        }

        Assert.Equal(HttpStatusCode.OK, await Search("198.51.100.20"));
        Assert.Equal(HttpStatusCode.TooManyRequests, await Search("198.51.100.20"));
        Assert.Equal(HttpStatusCode.OK, await Search("198.51.100.21"));
        // A forged leftmost address cannot create a fresh bucket for the first client.
        Assert.Equal(HttpStatusCode.TooManyRequests, await Search("192.0.2.99, 198.51.100.20"));
    }
}
