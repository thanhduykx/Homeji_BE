using Homeji.Infrastructure.Messaging;

namespace Homeji.Api.BackgroundJobs;

internal sealed class RentalPublicationAnalyticsWorker(IRentalPublicationAnalytics analytics) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        await analytics.ConsumeAsync(stoppingToken);
    }
}
