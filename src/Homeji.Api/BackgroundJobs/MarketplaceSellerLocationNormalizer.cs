using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.BackgroundJobs;

public sealed class MarketplaceSellerLocationNormalizer : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogNormalized =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(LogNormalized)),
            "Normalized {PostCount} legacy marketplace posts to one location per seller.");
    private static readonly Action<ILogger, Exception?> LogFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(LogFailed)),
            "Failed to normalize legacy marketplace seller locations.");

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MarketplaceSellerLocationNormalizer> _logger;

    public MarketplaceSellerLocationNormalizer(
        IServiceScopeFactory scopeFactory,
        ILogger<MarketplaceSellerLocationNormalizer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var changed = await dbContext.Database.ExecuteSqlRawAsync(
                """
                -- Repair the legacy demo seller first. Its historical seed mixed
                -- unrelated Hanoi and Thu Duc coordinates under one account.
                UPDATE homeji.marketplace_posts
                SET address = '01 Lưu Hữu Phước, Khu phố Tân Lập, phường Đông Hòa, TP.HCM',
                    latitude = 10.875340,
                    longitude = 106.800033
                WHERE seller_id = 'd3000000-0000-0000-0000-000000000021'
                  AND (address, latitude, longitude) IS DISTINCT FROM
                      ('01 Lưu Hữu Phước, Khu phố Tân Lập, phường Đông Hòa, TP.HCM', 10.875340, 106.800033);

                WITH anchors AS (
                    SELECT DISTINCT ON (seller_id)
                        seller_id,
                        address,
                        latitude,
                        longitude
                    FROM homeji.marketplace_posts
                    ORDER BY seller_id, created_at, id
                )
                UPDATE homeji.marketplace_posts AS post
                SET address = anchor.address,
                    latitude = anchor.latitude,
                    longitude = anchor.longitude
                FROM anchors AS anchor
                WHERE post.seller_id = anchor.seller_id
                  AND (post.address, post.latitude, post.longitude)
                      IS DISTINCT FROM (anchor.address, anchor.latitude, anchor.longitude);
                """,
                stoppingToken);
            LogNormalized(_logger, changed, null);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LogFailed(_logger, exception);
        }
    }
}
