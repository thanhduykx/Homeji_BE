using Homeji.Application.IServices.Emails;

namespace Homeji.Api.BackgroundJobs;

public sealed class AppointmentReminderWorker(IServiceScopeFactory scopeFactory, TimeProvider timeProvider,
    ILogger<AppointmentReminderWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(
        LogLevel.Error, new EventId(1, nameof(Failed)), "Appointment reminder dispatch failed; pending reminders remain in the database.");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), timeProvider);
        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IAppointmentReminderDispatcher>().DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { Failed(logger, exception); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
