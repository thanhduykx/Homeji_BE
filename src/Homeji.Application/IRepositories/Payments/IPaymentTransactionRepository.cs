using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.IRepositories.Payments;

public interface IPaymentTransactionRepository
{
    Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetByOrderCodeAsync(string orderCode, CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetByRequestIdAsync(string requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentTransaction>> GetForUserAsync(
        Guid userId,
        PaymentStatus? status,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(PaymentTransaction payment, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
