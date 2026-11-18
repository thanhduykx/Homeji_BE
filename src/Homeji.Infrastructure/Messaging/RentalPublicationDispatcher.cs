using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.Messaging;

internal sealed partial class RentalPublicationDispatcher(
    ApplicationDbContext db,
    IServiceProvider services,
    IOptions<RentalPublicationKafkaOptions> options,
    TimeProvider timeProvider,
    ILogger<RentalPublicationDispatcher> logger) : IRentalPublicationDispatcher
{
    private static readonly Meter Meter = new("Homeji.RentalPublications");
    private static readonly Counter<long> Published = Meter.CreateCounter<long>("homeji.rental_publications.published");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("homeji.rental_publications.publish_failures");
    private static readonly Histogram<double> PublishDelay = Meter.CreateHistogram<double>("homeji.rental_publications.publish_delay", "s");

    public async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lease = Guid.NewGuid();
        var batch = await db.Database.SqlQuery<ClaimedPublication>($"""
            WITH candidates AS (
                SELECT id FROM homeji.rental_publications
                WHERE published_at IS NULL AND available_at <= {now}
                ORDER BY available_at, id
                LIMIT 5 FOR UPDATE SKIP LOCKED
            )
            UPDATE homeji.rental_publications AS publication
            SET lease_id = {lease}, available_at = {now.AddMinutes(2)}, attempts = publication.attempts + 1
            FROM candidates WHERE publication.id = candidates.id
            RETURNING publication.id AS "Id", publication.rental_post_id AS "RentalPostId",
                publication.occurred_at AS "OccurredAt", publication.attempts AS "Attempts"
            """).ToListAsync(cancellationToken);

        foreach (var publication in batch)
        {
            var currentLease = db.RentalPublications.Where(item => item.Id == publication.Id && item.LeaseId == lease);
            if (options.Value.HasBroker)
            {
                try
                {
                    var producer = services.GetRequiredService<IProducer<string, string>>();
                    var delivery = await producer.ProduceAsync(options.Value.Topic, new Message<string, string>
                    {
                        Key = publication.RentalPostId.ToString(),
                        Value = new RentalPublicationEnvelope(publication.Id, RentalPublicationEnvelope.PublishedEventType,
                            RentalPublicationEnvelope.Version, publication.OccurredAt, publication.RentalPostId).Serialize()
                    }, cancellationToken);
                    if (delivery.Status != PersistenceStatus.Persisted)
                    {
                        await ScheduleRetryAsync(currentLease, publication.Attempts, cancellationToken);
                        LogPublishFailed(logger, "not_persisted");
                        continue;
                    }
                }
                catch (ProduceException<string, string> exception)
                {
                    await ScheduleRetryAsync(currentLease, publication.Attempts, cancellationToken);
                    LogPublishFailed(logger, exception.Error.Code.ToString());
                    continue;
                }
            }
            else if (options.Value.AnalyticsEnabled)
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO homeji.rental_publication_facts (event_id, rental_post_id, occurred_at)
                    VALUES ({publication.Id}, {publication.RentalPostId}, {publication.OccurredAt.ToUniversalTime()})
                    ON CONFLICT (event_id) DO NOTHING
                    """, cancellationToken);
            }

            var publishedAt = timeProvider.GetUtcNow();
            var acknowledged = await currentLease.ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.PublishedAt, publishedAt)
                .SetProperty(item => item.LeaseId, (Guid?)null), cancellationToken);
            if (acknowledged != 0)
            {
                Published.Add(1);
                PublishDelay.Record(Math.Max(0, (publishedAt - publication.OccurredAt).TotalSeconds));
            }
        }
    }

    private async Task ScheduleRetryAsync(IQueryable<RentalPublication> currentLease, int attempts, CancellationToken cancellationToken)
    {
        var retryAt = timeProvider.GetUtcNow().AddSeconds(1 << Math.Min(attempts, 8));
        await currentLease.ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.AvailableAt, retryAt)
            .SetProperty(item => item.LeaseId, (Guid?)null), cancellationToken);
        Failed.Add(1);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Kafka rental publication will be retried. Error: {ErrorCode}.")]
    private static partial void LogPublishFailed(ILogger logger, string errorCode);

    private sealed record ClaimedPublication(Guid Id, Guid RentalPostId, DateTimeOffset OccurredAt, int Attempts);
}
