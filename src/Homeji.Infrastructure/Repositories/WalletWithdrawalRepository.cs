using Homeji.Application.IRepositories.Wallets;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class WalletWithdrawalRepository : IWalletWithdrawalRepository
{
    private readonly ApplicationDbContext _dbContext;

    public WalletWithdrawalRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<WalletWithdrawalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _dbContext.WalletWithdrawalRequests.SingleOrDefaultAsync(request => request.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WalletWithdrawalRequest>> GetByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.WalletWithdrawalRequests.AsNoTracking()
            .Where(request => request.UserId == userId)
            .OrderByDescending(request => request.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WalletWithdrawalRequest>> GetForAdminAsync(
        WalletWithdrawalStatus? status,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.WalletWithdrawalRequests.AsNoTracking();
        if (status is not null) query = query.Where(request => request.Status == status);
        return await query.OrderByDescending(request => request.CreatedAt).Take(200).ToListAsync(cancellationToken);
    }

    public Task AddAsync(WalletWithdrawalRequest request, CancellationToken cancellationToken = default) =>
        _dbContext.WalletWithdrawalRequests.AddAsync(request, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _dbContext.SaveChangesAsync(cancellationToken);
}
