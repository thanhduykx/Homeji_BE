using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Homeji.Api.RateLimiting;

public static class RateLimitingPolicyNames
{
    /// <summary>Anonymous / public listing search — primary spam surface.</summary>
    public const string PublicSearch = "public-search";

    /// <summary>Public post detail reads.</summary>
    public const string PublicRead = "public-read";

    /// <summary>Login / register / email checks — stricter.</summary>
    public const string PublicAuth = "public-auth";
    /// <summary>Cookie session restore; runs on page loads, and shared networks share one address.</summary>
    public const string SessionCookie = "session-cookie";
    public const string WebsiteTraffic = "website-traffic";
    public const string CostlyOperations = "costly-operations";

    /// <summary>Anonymous MoMo and PayOS callbacks; generous so provider retries still get through.</summary>
    public const string PaymentWebhook = "payment-webhook";

    /// <summary>Anonymous browser CSP violation reports.</summary>
    public const string CspReports = "csp-reports";
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitWindowOptions PublicSearch { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitWindowOptions PublicRead { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
    public RateLimitWindowOptions PublicAuth { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
    public RateLimitWindowOptions SessionCookie { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
    public RateLimitWindowOptions CostlyOperations { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
}

public sealed class RateLimitWindowOptions
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}

public static class RateLimitingExtensions
{
    private const string ProxySecretSetting = "RateLimiting:ProxySecret";
    private const string ProxySecretHeader = "X-Homeji-Proxy-Secret";
    private const string ClientIpHeader = "X-Homeji-Client-IP";

    public static IServiceCollection AddHomejiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(RateLimitingOptions.SectionName)
            .Get<RateLimitingOptions>() ?? new RateLimitingOptions();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Quá nhiều yêu cầu",
                        Detail = "Bạn thao tác quá nhanh. Vui lòng thử lại sau ít phút.",
                        Instance = context.HttpContext.Request.Path,
                    },
                    options: (System.Text.Json.JsonSerializerOptions?)null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);
            };

            options.AddPolicy(
                RateLimitingPolicyNames.PublicSearch,
                httpContext => CreateIpPartition(httpContext, settings.PublicSearch));

            options.AddPolicy(
                RateLimitingPolicyNames.PublicRead,
                httpContext => CreateIpPartition(httpContext, settings.PublicRead));

            options.AddPolicy(
                RateLimitingPolicyNames.PublicAuth,
                httpContext => CreateIpPartition(httpContext, settings.PublicAuth));
            options.AddPolicy(RateLimitingPolicyNames.SessionCookie,
                httpContext => CreateIpPartition(httpContext, settings.SessionCookie));
            options.AddPolicy(RateLimitingPolicyNames.WebsiteTraffic,
                httpContext => CreateIpPartition(httpContext, new RateLimitWindowOptions { PermitLimit = 30, WindowSeconds = 60 }));
            options.AddPolicy(RateLimitingPolicyNames.CostlyOperations,
                httpContext => CreateIpPartition(httpContext, settings.CostlyOperations));
            options.AddPolicy(RateLimitingPolicyNames.PaymentWebhook,
                httpContext => CreateIpPartition(httpContext, new RateLimitWindowOptions { PermitLimit = 300, WindowSeconds = 60 }));
            options.AddPolicy(RateLimitingPolicyNames.CspReports,
                httpContext => CreateIpPartition(httpContext, new RateLimitWindowOptions { PermitLimit = 60, WindowSeconds = 60 }));
        });

        return services;
    }

    private static RateLimitPartition<string> CreateIpPartition(
        HttpContext httpContext,
        RateLimitWindowOptions window)
    {
        var permitLimit = Math.Max(1, window.PermitLimit);
        var windowSeconds = Math.Max(1, window.WindowSeconds);
        var partitionKey = ResolveClientKey(httpContext);

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
    }

    /// <summary>
    /// Prefer authenticated subject (higher trust), then the visitor named by our frontend proxy,
    /// else the connection address (requires ForwardedHeaders so Render/proxy IP is the real client).
    /// </summary>
    internal static string ResolveClientKey(HttpContext httpContext)
    {
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            var sub = httpContext.User.FindFirst("sub")?.Value
                ?? httpContext.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (!string.IsNullOrWhiteSpace(sub))
            {
                return $"user:{sub}";
            }
        }

        if (ProxiedClientIp(httpContext) is { } proxied)
        {
            return $"ip:{proxied}";
        }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "ip:unknown" : $"ip:{ip}";
    }

    /// <summary>
    /// Every browser request reaches the API through the frontend proxy, so its connection address is the
    /// proxy's. The address is believed only with the shared secret; otherwise anyone could pick a new quota.
    /// </summary>
    private static IPAddress? ProxiedClientIp(HttpContext httpContext)
    {
        var secret = httpContext.RequestServices.GetRequiredService<IConfiguration>()[ProxySecretSetting];
        if (string.IsNullOrEmpty(secret)) return null;
        var sent = httpContext.Request.Headers[ProxySecretHeader].ToString();
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(sent), Encoding.UTF8.GetBytes(secret))) return null;
        return IPAddress.TryParse(httpContext.Request.Headers[ClientIpHeader].ToString(), out var address) ? address : null;
    }
}
