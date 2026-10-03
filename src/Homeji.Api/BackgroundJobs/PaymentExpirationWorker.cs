using Homeji.Application.IRepositories.Payments;

namespace Homeji.Api.BackgroundJobs;

public sealed class PaymentExpirationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PaymentExpirationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogCancelled = LoggerMessage.Define<int>(
        LogLevel.Information, new EventId(1, nameof(PaymentExpirationWorker)),
        "Cancelled {PaymentCount} unpaid payments after 15 minutes.");
    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(
        LogLevel.Error, new EventId(2, nameof(PaymentExpirationWorker)), "Payment expiration sweep failed.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), timeProvider);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var payments = scope.ServiceProvider.GetRequiredService<IPaymentTransactionRepository>();
                var count = await payments.CancelOverdueAsync(timeProvider.GetUtcNow(), cancellationToken: stoppingToken);
                if (count > 0)
                {
                    LogCancelled(logger, count, null);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogFailure(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
