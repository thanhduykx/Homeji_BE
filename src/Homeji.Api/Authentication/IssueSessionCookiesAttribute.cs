using Homeji.Application.DTOs.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Homeji.Api.Authentication;

/// <summary>
/// Moves a freshly issued session into cookies when the client asks for cookie mode. Clients
/// that do not send the mode header keep receiving tokens in the body until they are retired.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class IssueSessionCookiesAttribute : TypeFilterAttribute
{
    public IssueSessionCookiesAttribute() : base(typeof(Filter))
    {
    }

    private sealed class Filter(SessionCookies cookies, ILogger<Filter> logger) : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var request = context.HttpContext.Request;
            if (!SessionCookies.RequestsCookieMode(request))
            {
                await next();
                return;
            }

            // Also stops login CSRF: a foreign page cannot sign the victim into another account.
            if (!cookies.IsTrustedRequest(request))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            var executed = await next();
            if (executed.Exception is not null
                || executed.Result is not ObjectResult { Value: AuthSessionDto { AccessToken: { } accessToken } session } result)
            {
                return;
            }

            if (!cookies.TryWrite(context.HttpContext.Response, accessToken, accessToken, session.RefreshToken))
            {
                SessionCookieLog.TokensTooLarge(logger, session.UserId);
                executed.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            result.Value = session with { RefreshToken = null };
        }
    }
}
