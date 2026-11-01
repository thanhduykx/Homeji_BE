using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.BackgroundJobs;

public sealed class AgentDraftCleanupWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<AgentDraftCleanupWorker> logger) : BackgroundService
{
    private static readonly Action<ILogger, Exception?> Failed = LoggerMessage.Define(LogLevel.Error,
        new EventId(1, nameof(Failed)), "Expired AI draft cleanup failed; retrying on the next interval.");
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = clock.GetUtcNow();
                await db.AgentPostDrafts.Where(row => row.ExpiresAt < now && row.PublishedPostId == null && !row.Cancelled)
                    .OrderBy(row => row.ExpiresAt).Take(500)
                    .ExecuteUpdateAsync(update => update.SetProperty(row => row.Payload, "{}").SetProperty(row => row.Cancelled, true), stoppingToken);
                await db.AgentPostDrafts.Where(row => row.CreatedAt < now.AddDays(-30) && row.PublishedPostId == null)
                    .OrderBy(row => row.CreatedAt).Take(500).ExecuteDeleteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { Failed(logger, error); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
