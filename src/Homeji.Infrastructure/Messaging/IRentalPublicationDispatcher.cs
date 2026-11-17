namespace Homeji.Infrastructure.Messaging;

public interface IRentalPublicationDispatcher
{
    Task DispatchBatchAsync(CancellationToken cancellationToken);
}
