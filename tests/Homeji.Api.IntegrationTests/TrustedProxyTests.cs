using System.Net;
using Homeji.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Api.IntegrationTests;

public sealed class TrustedProxyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_configured_proxy_can_change_client_ip_and_scheme(bool trusted)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(trusted
            ? new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = "192.0.2.10" }
            : []).Build();
        var services = new ServiceCollection().AddLogging().AddTrustedProxyHeaders(configuration);
        await using var provider = services.BuildServiceProvider();
        var middleware = new ForwardedHeadersMiddleware(_ => Task.CompletedTask,
            provider.GetRequiredService<ILoggerFactory>(),
            provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        context.Request.Scheme = "http";
        context.Request.Headers["X-Forwarded-For"] = "198.51.100.20";
        context.Request.Headers["X-Forwarded-Proto"] = "https";

        await middleware.Invoke(context);

        Assert.Equal(trusted ? "198.51.100.20" : "192.0.2.10", context.Connection.RemoteIpAddress.ToString());
        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
    }

    [Fact]
    public void Unrestricted_network_is_rejected()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["ReverseProxy:KnownNetworks:0"] = "0.0.0.0/0" }).Build();
        using var provider = new ServiceCollection().AddTrustedProxyHeaders(configuration).BuildServiceProvider();
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value);
    }
}
