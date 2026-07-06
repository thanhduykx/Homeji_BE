namespace Homeji.Application.IRepositories.Moderation;

public interface IBadWordRepository
{
    Task<IReadOnlyList<string>> GetActiveValuesAsync(CancellationToken cancellationToken = default);
}
