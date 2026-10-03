using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Homeji.Api.Security;

public static class TrustedProxyExtensions
{
    public static IServiceCollection AddTrustedProxyHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Keep the framework's loopback defaults. Never trust arbitrary client headers.
            foreach (var value in configuration.GetSection("ReverseProxy:KnownProxies").GetChildren())
            {
                if (!IPAddress.TryParse(value.Value, out var address))
                {
                    throw new InvalidOperationException("ReverseProxy:KnownProxies must contain IP addresses.");
                }

                options.KnownProxies.Add(address);
            }

            foreach (var value in configuration.GetSection("ReverseProxy:KnownNetworks").GetChildren())
            {
                if (!System.Net.IPNetwork.TryParse(value.Value, out var network) || network.PrefixLength == 0)
                {
                    throw new InvalidOperationException("ReverseProxy:KnownNetworks must contain restricted CIDR networks.");
                }

                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
                    network.BaseAddress, network.PrefixLength));
            }
        });

        return services;
    }
}
