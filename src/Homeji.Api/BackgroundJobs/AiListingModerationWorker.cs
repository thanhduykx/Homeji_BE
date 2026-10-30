using Homeji.Application.IServices.Moderation;
namespace Homeji.Api.BackgroundJobs;
public sealed class AiListingModerationWorker(IServiceScopeFactory scopes, ILogger<AiListingModerationWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> LogFailure = LoggerMessage.Define(LogLevel.Error, new EventId(1, nameof(AiListingModerationWorker)), "AI listing review worker failed; persisted jobs remain available for recovery.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<IAiListingModeration>().TickAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { LogFailure(logger, exception); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
