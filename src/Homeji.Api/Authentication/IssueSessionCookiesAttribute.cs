using Homeji.Application.DTOs.Accounts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Homeji.Api.Authentication;

/// <summary>
/// Moves a freshly issued session into cookies when the client asks for cookie mode. The refresh
/// token never appears in the body: clients without the mode header get only the short-lived
/// access token and no cookies.
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
            var cookieMode = SessionCookies.RequestsCookieMode(request);

            // Also stops login CSRF: a foreign page cannot sign the victim into another account.
            if (cookieMode && !cookies.IsTrustedRequest(request))
            {
                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            var executed = await next();
            if (executed.Exception is not null
                || executed.Result is not ObjectResult { Value: AuthSessionDto session } result)
            {
                return;
            }

            if (cookieMode && session.AccessToken is { } accessToken
                && !cookies.TryWrite(context.HttpContext.Response, accessToken, accessToken, session.RefreshToken))
            {
                SessionCookieLog.TokensTooLarge(logger, session.UserId);
                executed.Result = new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
                return;
            }

            result.Value = session with { RefreshToken = null };
        }
    }
}
