using Homeji.Application.DTOs.Wallets;
using Homeji.Domain.Enums;

namespace Homeji.Application.IServices.Wallets;

public interface IWalletWithdrawalService
{
    Task<WalletWithdrawalDto> CreateAsync(CreateWalletWithdrawalDto request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletWithdrawalDto>> GetMineAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletWithdrawalDto>> GetForAdminAsync(WalletWithdrawalStatus? status, CancellationToken cancellationToken = default);
    Task<WalletWithdrawalDto> CompleteAsync(Guid id, ReviewWalletWithdrawalDto request, CancellationToken cancellationToken = default);
    Task<WalletWithdrawalDto> RejectAsync(Guid id, ReviewWalletWithdrawalDto request, CancellationToken cancellationToken = default);
}
