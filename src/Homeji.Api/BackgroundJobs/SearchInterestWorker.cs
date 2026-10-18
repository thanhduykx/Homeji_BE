using Homeji.Application.IServices.RentalPosts;
namespace Homeji.Api.BackgroundJobs;

public sealed class SearchInterestWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<SearchInterestWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(LogLevel.Error,
        new EventId(1, nameof(Failed)), "Search interest matching failed; the next pass will continue.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        do
        {
            try { await using var scope = scopes.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<ISearchInterestService>().ProcessBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { Failed(logger, e); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
