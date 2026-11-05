using Microsoft.Extensions.Options;

namespace Homeji.Api.Authentication;

public sealed class AuthCookieOptions
{
    public const string SectionName = "AuthCookies";

    /// <summary>
    /// Browser origins allowed to use cookie sessions. Kept apart from Cors:AllowedOrigins,
    /// which also lists local development hosts in the Development environment.
    /// </summary>
    public string[] AllowedOrigins { get; set; } = [];

    public int LifetimeDays { get; set; } = 90;
}

/// <summary>
/// The browser session lives in host-only cookies on the frontend origin, because the production
/// API is reached through the frontend's same-origin proxy. onrender.com is a public suffix, so
/// cookies set directly by the API host would be third-party and are blocked by several browsers.
/// </summary>
public sealed class SessionCookies(IOptions<AuthCookieOptions> options)
{
    public const string RefreshCookie = "__Secure-homeji_rt";
    public const string AnchorCookie = "__Secure-homeji_anchor";
    public const string AccessCookie = "__Secure-homeji_at";

    /// <summary>Holds no secret: it only tells the frontend a session may exist, so guests skip the API call.</summary>
    public const string HintCookie = "homeji_session_hint";
    public const string ModeHeader = "X-Homeji-Session";
    public const string CookieMode = "cookie";

    private const string SessionPath = "/api/account/session";

    /// <summary>Browsers drop cookies above 4096 bytes silently, which would sign the user out on reload.</summary>
    private const int MaxValueLength = 3800;

    public string? Refresh(HttpRequest request) => request.Cookies[RefreshCookie];

    public string? Anchor(HttpRequest request) => request.Cookies[AnchorCookie];

    public string? Access(HttpRequest request) => request.Cookies[AccessCookie];

    public static bool RequestsCookieMode(HttpRequest request) =>
        string.Equals(request.Headers[ModeHeader], CookieMode, StringComparison.Ordinal);

    /// <summary>
    /// CSRF defence for every endpoint that reads or sets these cookies, alongside SameSite=Strict.
    /// The custom header forces a CORS preflight and the origin must be the frontend itself.
    /// </summary>
    public bool IsTrustedRequest(HttpRequest request)
    {
        if (!RequestsCookieMode(request)) return false;
        var origin = request.Headers.Origin.ToString();
        return origin.Length > 0
            && options.Value.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns false, writing nothing, when a token cannot fit in a cookie.</summary>
    public bool TryWrite(HttpResponse response, string anchor, string accessToken, string? refreshToken)
    {
        if (anchor.Length > MaxValueLength || accessToken.Length > MaxValueLength
            || refreshToken?.Length > MaxValueLength) return false;
        var secret = SecretCookie();
        response.Cookies.Append(AnchorCookie, anchor, secret);
        response.Cookies.Append(AccessCookie, accessToken, secret);
        if (refreshToken is null) response.Cookies.Delete(RefreshCookie, secret);
        else response.Cookies.Append(RefreshCookie, refreshToken, secret);
        response.Cookies.Append(HintCookie, "1", HintOptions());
        return true;
    }

    public void Clear(HttpResponse response)
    {
        var secret = SecretCookie();
        response.Cookies.Delete(RefreshCookie, secret);
        response.Cookies.Delete(AnchorCookie, secret);
        response.Cookies.Delete(AccessCookie, secret);
        response.Cookies.Delete(HintCookie, HintOptions());
    }

    private CookieOptions SecretCookie() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = SessionPath,
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(options.Value.LifetimeDays),
    };

    private CookieOptions HintOptions() => new()
    {
        HttpOnly = false,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(options.Value.LifetimeDays),
    };
}
