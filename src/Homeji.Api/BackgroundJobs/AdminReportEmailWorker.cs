using Homeji.Application.IServices.Admin;

namespace Homeji.Api.BackgroundJobs;

public sealed class AdminReportEmailWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AdminReportEmailWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(LogLevel.Error,
        new EventId(1, nameof(Failed)), "Admin report email dispatch failed; the durable export remains available.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IAdminReportEmailDispatcher>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { Failed(logger, error); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
