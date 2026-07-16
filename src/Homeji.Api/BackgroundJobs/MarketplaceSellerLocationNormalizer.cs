using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Api.BackgroundJobs;

public sealed class MarketplaceSellerLocationNormalizer : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> LogNormalized =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, nameof(LogNormalized)),
            "Normalized {PostCount} legacy demo post locations to the Homeji service area.");
    private static readonly Action<ILogger, Exception?> LogFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, nameof(LogFailed)),
            "Failed to normalize legacy demo post locations.");

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
                -- Each seller has one canonical Thu Duc-area location. The
                -- historical demo mixed Hanoi and Thu Duc under these accounts.
                WITH seller_locations(seller_id, address, latitude, longitude) AS (
                    VALUES
                        ('d3000000-0000-0000-0000-000000000021'::uuid,
                         '01 Lưu Hữu Phước, Khu phố Tân Lập, phường Đông Hòa, TP.HCM', 10.875340::numeric, 106.800033::numeric),
                        ('d3000000-0000-0000-0000-000000000022'::uuid,
                         '12 Lưu Hữu Phước, phường Đông Hòa, TP.HCM', 10.874820::numeric, 106.800610::numeric),
                        ('d3000000-0000-0000-0000-000000000024'::uuid,
                         'Lô E2a-7, Đường D1, Khu Công nghệ cao, phường Tăng Nhơn Phú, TP.HCM', 10.841350::numeric, 106.809950::numeric)
                )
                UPDATE homeji.marketplace_posts AS post
                SET address = location.address,
                    latitude = location.latitude,
                    longitude = location.longitude
                FROM seller_locations AS location
                WHERE post.seller_id = location.seller_id
                  AND (post.address, post.latitude, post.longitude)
                      IS DISTINCT FROM (location.address, location.latitude, location.longitude);

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

                WITH rental_locations(id, address, latitude, longitude) AS (
                    VALUES
                        ('f5a69bc2-ffed-4fe5-a6b9-7c9d712b85b7'::uuid,
                         'Đường D1, phường Long Thạnh Mỹ, TP. Thủ Đức, TP.HCM', 10.8421800::numeric, 106.8097000::numeric),
                        ('391b0b6e-2f78-4413-abc7-3268de7ad576'::uuid,
                         'Đường D2, phường Long Thạnh Mỹ, TP. Thủ Đức, TP.HCM', 10.8446200::numeric, 106.8078500::numeric),
                        ('c96829e9-e4b5-436f-b7a5-f108879d9e65'::uuid,
                         'Đường Man Thiện, phường Tăng Nhơn Phú, TP.HCM', 10.8499300::numeric, 106.7892400::numeric),
                        ('0d9948fd-73f4-450b-a24c-30cb43140cf5'::uuid,
                         'Đường D1, phường Long Thạnh Mỹ, TP. Thủ Đức, TP.HCM', 10.8435100::numeric, 106.8090300::numeric),
                        ('4979493d-6cc5-417c-b936-fa91410f82b8'::uuid,
                         'Đường số 12, phường Linh Trung, TP. Thủ Đức, TP.HCM', 10.8662100::numeric, 106.7768200::numeric),
                        ('653aaa8a-f586-40e6-8934-20971dcf7bb7'::uuid,
                         'Đường Lê Văn Việt, phường Tăng Nhơn Phú, TP.HCM', 10.8476500::numeric, 106.7824200::numeric),
                        ('e2222222-2222-2222-2222-222222222222'::uuid,
                         'Đường Tạ Quang Bửu, phường Đông Hòa, TP.HCM', 10.8799000::numeric, 106.8041000::numeric)
                )
                UPDATE homeji.rental_posts AS post
                SET address = location.address,
                    latitude = location.latitude,
                    longitude = location.longitude,
                    updated_at = NOW()
                FROM rental_locations AS location
                WHERE post.id = location.id
                  AND (post.address, post.latitude, post.longitude)
                      IS DISTINCT FROM (location.address, location.latitude, location.longitude);

                UPDATE homeji.rental_posts
                SET title = 'Phòng ở ghép tiện nghi gần ĐH Bách Khoa - khu ĐHQG',
                    updated_at = NOW()
                WHERE id = 'e2222222-2222-2222-2222-222222222222'
                  AND title IS DISTINCT FROM 'Phòng ở ghép tiện nghi gần ĐH Bách Khoa - khu ĐHQG';
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
