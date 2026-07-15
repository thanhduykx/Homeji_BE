using Homeji.Application.DTOs.Wallets;

namespace Homeji.Application.IServices.Wallets;

public interface IWalletService
{
    Task<WalletDto> GetMineAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WalletTransactionDto>> GetMyTransactionsAsync(int take, CancellationToken cancellationToken = default);
}
