using Homeji.Infrastructure.Messaging;

namespace Homeji.Api.BackgroundJobs;

internal sealed partial class RentalPublicationWorker(
    IServiceScopeFactory scopes,
    TimeProvider timeProvider,
    ILogger<RentalPublicationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRentalPublicationDispatcher>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogDispatchFailed(logger, exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Rental publication dispatch failed. Pending events remain in PostgreSQL.")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception);
}
