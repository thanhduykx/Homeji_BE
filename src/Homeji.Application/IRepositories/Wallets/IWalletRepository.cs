using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Wallets;

public interface IWalletRepository
{
    Task<WalletAccount?> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(Guid userId, int take, CancellationToken cancellationToken = default);
    Task<bool> HasTransactionAsync(Guid userId, WalletTransactionKind kind, Guid referenceId, CancellationToken cancellationToken = default);
    Task AddAccountAsync(WalletAccount account, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(WalletTransaction transaction, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
