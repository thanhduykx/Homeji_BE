using Homeji.Application.IServices.Emails;

namespace Homeji.Api.BackgroundJobs;

public sealed class ReceiptEmailWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<ReceiptEmailWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(
        LogLevel.Error, new EventId(1, nameof(Failed)), "Receipt email dispatch failed; durable jobs remain available for retry.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), timeProvider);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IReceiptEmailDispatcher>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { Failed(logger, exception); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
