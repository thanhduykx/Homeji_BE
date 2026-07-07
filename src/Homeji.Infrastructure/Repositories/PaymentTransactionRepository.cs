using Homeji.Application.IRepositories.Payments;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

public sealed class PaymentTransactionRepository : IPaymentTransactionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public PaymentTransactionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentTransactions.SingleOrDefaultAsync(payment => payment.Id == id, cancellationToken);
    }

    public Task<PaymentTransaction?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentTransactions.SingleOrDefaultAsync(payment => payment.OrderCode == orderCode, cancellationToken);
    }

    public Task<PaymentTransaction?> GetByRequestIdAsync(string requestId, CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentTransactions.SingleOrDefaultAsync(payment => payment.RequestId == requestId, cancellationToken);
    }

    public async Task AddAsync(PaymentTransaction payment, CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentTransactions.AddAsync(payment, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
