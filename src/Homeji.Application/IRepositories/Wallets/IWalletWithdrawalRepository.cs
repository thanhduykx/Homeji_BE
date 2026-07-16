using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Wallets;

public interface IWalletWithdrawalRepository
{
    Task<WalletWithdrawalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletWithdrawalRequest>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletWithdrawalRequest>> GetForAdminAsync(WalletWithdrawalStatus? status, CancellationToken cancellationToken = default);
    Task AddAsync(WalletWithdrawalRequest request, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
