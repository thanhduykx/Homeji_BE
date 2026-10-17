using Homeji.Application.DTOs.Emails;
using Homeji.Domain.Entities;

namespace Homeji.Application.IServices.Emails;

public interface ITransactionReceiptService
{
    // Records tracked entities only; callers save them with the financial state in one transaction.
    Task RecordPaymentAsync(PaymentTransaction payment, CancellationToken cancellationToken);
    Task RecordWithdrawalAsync(WalletWithdrawalRequest withdrawal, CancellationToken cancellationToken);
    Task<ReceiptPageDto> GetMineAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<TransactionReceiptDto> GetMineByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<string> GetMyHtmlAsync(Guid id, CancellationToken cancellationToken);
}
