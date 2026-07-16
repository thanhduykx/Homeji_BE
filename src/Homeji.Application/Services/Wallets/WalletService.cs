using Homeji.Application.DTOs.Wallets;
using Homeji.Application.IRepositories.Wallets;
using Homeji.Application.IServices.Wallets;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;

namespace Homeji.Application.Services.Wallets;

public sealed class WalletService : IWalletService
{
    private readonly UserContext _userContext;
    private readonly IWalletRepository _wallets;

    public WalletService(UserContext userContext, IWalletRepository wallets)
    {
        _userContext = userContext;
        _wallets = wallets;
    }

    public async Task<WalletDto> GetMineAsync(CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var wallet = await _wallets.GetAsync(userId, cancellationToken);
        return wallet is null
            ? new WalletDto(userId, 0, 0, 0, 0, false, WalletAccount.MinimumTopUp, WalletAccount.MaximumTopUp, WalletAccount.MinimumWithdrawalReserve, null)
            : ToDto(wallet);
    }

    public async Task<IReadOnlyList<WalletTransactionDto>> GetMyTransactionsAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var transactions = await _wallets.GetTransactionsAsync(
            _userContext.GetRequiredUserId(),
            Math.Clamp(take, 1, 100),
            cancellationToken);
        return transactions.Select(transaction => new WalletTransactionDto(
            transaction.Id,
            transaction.Kind,
            transaction.Amount,
            transaction.BalanceAfter,
            transaction.ReferenceId,
            transaction.Description,
            transaction.CreatedAt)).ToArray();
    }

    private static WalletDto ToDto(WalletAccount wallet) => new(
        wallet.UserId,
        wallet.Balance,
        wallet.TotalDeposited,
        wallet.TotalSpent,
        wallet.TotalEarned,
        wallet.IsActivated,
        WalletAccount.MinimumTopUp,
        WalletAccount.MaximumTopUp,
        WalletAccount.MinimumWithdrawalReserve,
        wallet.UpdatedAt);
}
