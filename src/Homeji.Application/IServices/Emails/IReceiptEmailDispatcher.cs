namespace Homeji.Application.IServices.Emails;

public interface IReceiptEmailDispatcher
{
    Task DispatchBatchAsync(CancellationToken cancellationToken);
}
