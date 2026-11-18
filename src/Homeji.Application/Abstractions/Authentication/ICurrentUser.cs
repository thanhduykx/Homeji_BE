namespace Homeji.Application.Abstractions.Authentication;

public interface ICurrentUser
{
    Guid? UserId { get; }

    string? Email => null;
}
