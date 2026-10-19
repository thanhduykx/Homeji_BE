using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.Repositories;

internal static class FinancialTransaction
{
    // Same lock namespace as PremiumWalletPaymentService, across all API instances.
    public static Task<T> ExecuteAsync<T>(ApplicationDbContext db, Guid userId, Func<Task<T>> action, CancellationToken cancellationToken) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var key = userId.ToString("N");
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
}
