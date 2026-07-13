using Homeji.Application.IServices.Activities;

namespace Homeji.Api.Middlewares;

public sealed class UserActivityMiddleware
{
    private static readonly Action<ILogger, Guid, Exception?> RecordFailed =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(1, nameof(InvokeAsync)),
            "Could not record activity for user {UserId}.");

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UserActivityMiddleware> _logger;

    public UserActivityMiddleware(
        RequestDelegate next,
        IServiceScopeFactory scopeFactory,
        ILogger<UserActivityMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (!IsSuccessfulMutation(context)
            || !Guid.TryParse(context.User.FindFirst("sub")?.Value, out var userId))
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var activities = scope.ServiceProvider.GetRequiredService<IUserActivityService>();
            var action = context.GetEndpoint()?.DisplayName ?? $"{context.Request.Method} {context.Request.Path}";
            await activities.RecordAsync(
                userId,
                action,
                context.Request.Path.Value ?? "/",
                context.Request.Method,
                context.Response.StatusCode,
                cancellationToken: CancellationToken.None);
        }
        catch (Exception exception)
        {
            RecordFailed(_logger, userId, exception);
        }
    }

    private static bool IsSuccessfulMutation(HttpContext context)
    {
        return context.Response.StatusCode is >= 200 and < 400
            && context.Request.Method is "POST" or "PUT" or "PATCH" or "DELETE";
    }
}
