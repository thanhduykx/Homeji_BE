namespace Homeji.Application.Common.Exceptions;

public sealed class ForbiddenAccessException : Exception
{
    public string? Code { get; }

    public ForbiddenAccessException(string message, string? code = null)
        : base(message)
    {
        Code = code;
    }
}
