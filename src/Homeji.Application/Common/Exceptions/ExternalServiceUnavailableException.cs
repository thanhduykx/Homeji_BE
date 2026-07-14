namespace Homeji.Application.Common.Exceptions;

public sealed class ExternalServiceUnavailableException : Exception
{
    public ExternalServiceUnavailableException(
        string serviceName,
        string message,
        TimeSpan? retryAfter = null)
        : base(message)
    {
        ServiceName = serviceName;
        RetryAfter = retryAfter;
    }

    public string ServiceName { get; }

    public TimeSpan? RetryAfter { get; }
}
