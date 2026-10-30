namespace Homeji.Application.IServices.Payments;

public interface IExpiredPaymentCleanup
{
    Task<int> DeleteConfirmedUnpaidAsync(CancellationToken cancellationToken = default);
}
