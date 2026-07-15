using Homeji.Application.IRepositories.Wallets;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class WalletRepository : IWalletRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WalletRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<WalletAccount?> GetAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _dbContext.WalletAccounts.SingleOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<WalletTransaction>> GetTransactionsAsync(
        Guid userId,
        int take,
        CancellationToken cancellationToken = default) =>
        await _dbContext.WalletTransactions.AsNoTracking()
            .Where(transaction => transaction.WalletUserId == userId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<bool> HasTransactionAsync(
        Guid userId,
        WalletTransactionKind kind,
        Guid referenceId,
        CancellationToken cancellationToken = default) =>
        _dbContext.WalletTransactions.AnyAsync(
            transaction => transaction.WalletUserId == userId
                && transaction.Kind == kind
                && transaction.ReferenceId == referenceId,
            cancellationToken);

    public Task AddAccountAsync(WalletAccount account, CancellationToken cancellationToken = default) =>
        _dbContext.WalletAccounts.AddAsync(account, cancellationToken).AsTask();

    public Task AddTransactionAsync(WalletTransaction transaction, CancellationToken cancellationToken = default) =>
        _dbContext.WalletTransactions.AddAsync(transaction, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
