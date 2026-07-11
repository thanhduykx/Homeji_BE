namespace Homeji.Application.IRepositories.Accounts;

public interface IAccountEmailRepository
{
    Task<bool> ExistsAsync(string normalizedEmail, CancellationToken cancellationToken = default);
}
