using System.Security.Claims;
using Homeji.Application.Abstractions.Authentication;

namespace Homeji.Api.Authentication;

public sealed class HttpCurrentUser : ICurrentUser
{
    public string? Email => _httpContextAccessor.HttpContext?.User.FindFirstValue("email");

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var subject = _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(subject, out var userId) ? userId : null;
        }
    }
}
