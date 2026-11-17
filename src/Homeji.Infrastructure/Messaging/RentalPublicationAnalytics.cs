using System.Diagnostics.Metrics;
using Confluent.Kafka;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Homeji.Infrastructure.Messaging;

internal sealed partial class RentalPublicationAnalytics(
    IServiceScopeFactory scopes,
    IOptions<RentalPublicationKafkaOptions> options,
    TimeProvider timeProvider,
    ILogger<RentalPublicationAnalytics> logger) : IRentalPublicationAnalytics
{
    private static readonly Meter Meter = new("Homeji.RentalPublications");
    private static readonly Counter<long> Recorded = Meter.CreateCounter<long>("homeji.rental_publications.analytics_recorded");
    private static readonly Counter<long> Replayed = Meter.CreateCounter<long>("homeji.rental_publications.analytics_replayed");
    private static readonly Counter<long> Invalid = Meter.CreateCounter<long>("homeji.rental_publications.analytics_invalid");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>("homeji.rental_publications.analytics_failures");

    public async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var config = new ConsumerConfig(settings.CreateClientConfig())
        {
            ClientId = "homeji-rental-publication-analytics",
            GroupId = settings.AnalyticsGroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
            EnableAutoOffsetStore = false,
            AllowAutoCreateTopics = false
        };

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var consumer = new ConsumerBuilder<string, string>(config).Build();
                consumer.Subscribe(settings.Topic);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var message = consumer.Consume(cancellationToken);
                        await RecordAsync(message, cancellationToken);
                        // The next consumer can safely replay the record if committing this offset fails.
                        consumer.Commit(message);
                    }
                }
                finally
                {
                    consumer.Close();
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                Failed.Add(1);
                LogConsumerFailure(logger, exception);
                await Task.Delay(TimeSpan.FromSeconds(5), timeProvider, cancellationToken);
            }
        }
    }

    private async Task RecordAsync(ConsumeResult<string, string> message, CancellationToken cancellationToken)
    {
        var envelope = RentalPublicationEnvelope.Read(message.Message.Key, message.Message.Value);
        if (envelope is null)
        {
            Invalid.Add(1);
            LogInvalidEvent(logger, message.Partition.Value, message.Offset.Value);
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO homeji.rental_publication_facts (event_id, rental_post_id, occurred_at)
            VALUES ({envelope.EventId}, {envelope.ListingId}, {envelope.OccurredAt.ToUniversalTime()})
            ON CONFLICT (event_id) DO NOTHING
            """, cancellationToken);
        (inserted == 0 ? Replayed : Recorded).Add(1);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rental publication analytics ignored an invalid event at partition {Partition}, offset {Offset}.")]
    private static partial void LogInvalidEvent(ILogger logger, int partition, long offset);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rental publication analytics will reconnect and replay from its last committed offset.")]
    private static partial void LogConsumerFailure(ILogger logger, Exception exception);
}
