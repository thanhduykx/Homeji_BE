namespace Homeji.Infrastructure.Messaging;

public interface IRentalPublicationAnalytics
{
    Task ConsumeAsync(CancellationToken cancellationToken);
}
