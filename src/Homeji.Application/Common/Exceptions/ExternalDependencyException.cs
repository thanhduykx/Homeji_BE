namespace Homeji.Application.Common.Exceptions;

/// <summary>
/// Upstream dependency (e.g. Gemini) failed or is misconfigured.
/// Mapped to HTTP 503 — not a client validation error.
/// </summary>
public sealed class ExternalDependencyException : Exception
{
    public ExternalDependencyException(string message)
        : base(message)
    {
    }

    public ExternalDependencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
