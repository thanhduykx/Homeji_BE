using Homeji.Application.IServices.MarketplaceOrders;

namespace Homeji.Api.BackgroundJobs;

public sealed class MarketplaceOrderExpirationWorker : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private static readonly Action<ILogger, int, Exception?> LogExpiredOrders =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(MarketplaceOrderExpirationWorker)),
            "Expired {OrderCount} overdue marketplace orders.");
    private static readonly Action<ILogger, Exception?> LogSweepFailure =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(MarketplaceOrderExpirationWorker)),
            "Marketplace order expiration sweep failed.");
    private static readonly Action<ILogger, int, Exception?> LogReleasedOrders =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(3, nameof(MarketplaceOrderExpirationWorker)),
            "Released funds for {OrderCount} marketplace orders after the escrow hold.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketplaceOrderExpirationWorker> _logger;

    public MarketplaceOrderExpirationWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<MarketplaceOrderExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var expirationService = scope.ServiceProvider
                    .GetRequiredService<IMarketplaceOrderExpirationService>();
                var expiredCount = await expirationService.ExpireOverdueAsync(stoppingToken);
                if (expiredCount > 0)
                {
                    LogExpiredOrders(_logger, expiredCount, null);
                }

                var releasedCount = await expirationService.ReleaseOverdueFundsAsync(stoppingToken);
                if (releasedCount > 0)
                {
                    LogReleasedOrders(_logger, releasedCount, null);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSweepFailure(_logger, exception);
            }
        }
    }
}
