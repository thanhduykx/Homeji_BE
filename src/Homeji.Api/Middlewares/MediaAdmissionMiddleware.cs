using Homeji.Infrastructure.Health;
using Microsoft.AspNetCore.Mvc;

namespace Homeji.Api.Middlewares;

/// <summary>Rejects excess media bodies before MVC buffers/deserializes them.</summary>
public sealed class MediaAdmissionMiddleware(RequestDelegate next) : IDisposable
{
    private readonly SemaphoreSlim _slot = new(1, 1);
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        var mediaRequest = HttpMethods.IsPost(context.Request.Method)
            && (path.StartsWithSegments("/api/chatbot") || path.StartsWithSegments("/api/upload")
                || path.StartsWithSegments("/api/conversations") && (path.Value!.EndsWith("/images", StringComparison.OrdinalIgnoreCase)
                    || path.Value.EndsWith("/voice", StringComparison.OrdinalIgnoreCase)));
        if (!mediaRequest) { await next(context); return; }
        if (!await _slot.WaitAsync(0, context.RequestAborted)) { await RejectAsync(context); return; }
        try
        {
            if (MediaMemoryPressure.IsHigh()) { await RejectAsync(context); return; }
            await next(context);
        }
        finally { _slot.Release(); }
    }

    private static Task RejectAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = "5";
        return context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = 429, Title = "Hệ thống đang xử lý ảnh/AI", Detail = "Vui lòng giữ ảnh đã chọn và thử lại sau vài giây.",
        }, context.RequestAborted);
    }

    public void Dispose() => _slot.Dispose();
}
