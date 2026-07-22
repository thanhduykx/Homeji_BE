using System.Globalization;
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
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public RateLimitWindowOptions PublicSearch { get; set; } = new() { PermitLimit = 60, WindowSeconds = 60 };
    public RateLimitWindowOptions PublicRead { get; set; } = new() { PermitLimit = 120, WindowSeconds = 60 };
    public RateLimitWindowOptions PublicAuth { get; set; } = new() { PermitLimit = 20, WindowSeconds = 60 };
}

public sealed class RateLimitWindowOptions
{
    public int PermitLimit { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}

public static class RateLimitingExtensions
{
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
                    cancellationToken);
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
    /// Prefer authenticated subject (higher trust), else client IP
    /// (requires ForwardedHeaders so Render/proxy IP is the real client).
    /// </summary>
    private static string ResolveClientKey(HttpContext httpContext)
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

        var ip = httpContext.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(ip) ? "ip:unknown" : $"ip:{ip}";
    }
}
