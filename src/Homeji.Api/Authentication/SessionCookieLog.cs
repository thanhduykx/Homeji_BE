namespace Homeji.Api.Authentication;

internal static partial class SessionCookieLog
{
    [LoggerMessage(EventId = 7301, Level = LogLevel.Error, Message = "Session tokens do not fit in cookies for user {UserId}.")]
    public static partial void TokensTooLarge(ILogger logger, Guid? userId);

    [LoggerMessage(EventId = 7302, Level = LogLevel.Warning, Message = "Provider sign-out failed; cookies were still cleared.")]
    public static partial void ProviderSignOutFailed(ILogger logger, Exception? exception);
}
