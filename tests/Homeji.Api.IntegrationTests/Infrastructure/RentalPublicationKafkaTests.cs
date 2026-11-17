using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.AI;
using Homeji.Application.IServices.AI;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class RentalPublicationKafkaTests
{
    [RentalPublicationKafkaFact]
    public async Task Approval_survives_broker_failure_and_analytics_records_one_fact_after_replays()
    {
        var dbSettings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE"));
        var kafka = Environment.GetEnvironmentVariable("HOMEJI_TEST_KAFKA");
        if (dbSettings.Host != "127.0.0.1" || dbSettings.Database != "homeji_quality" || kafka != "127.0.0.1:19092")
        {
            throw new InvalidOperationException("Use the isolated Homeji PostgreSQL and Kafka test endpoints.");
        }

        var runId = Guid.NewGuid().ToString("N");
        dbSettings.Database = $"homeji_publications_{runId}";
        var topic = $"homeji-publications-test-{runId}";
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(dbSettings.ConnectionString).Options;
        var parentSettings = new NpgsqlConnectionStringBuilder(dbSettings.ConnectionString) { Database = "postgres" };
        await using var parent = new NpgsqlConnection(parentSettings.ConnectionString);
        await parent.OpenAsync();
        // The interpolated identifier consists of a fixed prefix and a generated hexadecimal GUID.
        await using (var create = new NpgsqlCommand($"CREATE DATABASE {dbSettings.Database}", parent))
        {
            await create.ExecuteNonQueryAsync();
        }

        using var admin = new AdminClientBuilder(new AdminClientConfig { BootstrapServers = kafka }).Build();
        var topicCreated = false;
        try
        {
            await using var db = new ApplicationDbContext(dbOptions);
            var (actor, postId) = await SeedAsync(db);
            using var offlineApi = CreateApi(dbSettings.ConnectionString, "127.0.0.1:19093", topic, actor);
            using var client = offlineApi.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            var endpoint = new Uri($"/api/admin/moderation/rental-posts/{postId}/approve", UriKind.Relative);
            var approval = client.PostAsJsonAsync(endpoint, new ApproveRentalPostDto(null));
            var duplicate = client.PostAsJsonAsync(endpoint, new ApproveRentalPostDto(null));
            using var first = await approval;
            using var second = await duplicate;
            Assert.Single(new[] { first, second }, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Contains(new[] { first, second }, response => response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity);

            Assert.Equal(RentalPostStatus.Active, await db.RentalPosts.AsNoTracking().Where(item => item.Id == postId).Select(item => item.Status).SingleAsync());
            var publication = Assert.Single(await ReadOutboxAsync(db, postId));
            Assert.Null(publication.PublishedAt);
            Assert.Equal(0, publication.Attempts);

            await DispatchAsync(offlineApi);
            var pendingRetry = Assert.Single(await ReadOutboxAsync(db, postId));
            Assert.Equal(publication.Id, pendingRetry.Id);
            Assert.Null(pendingRetry.PublishedAt);
            Assert.Equal(1, pendingRetry.Attempts);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE homeji.rental_publications SET available_at = now() WHERE id = {publication.Id}");

            await admin.CreateTopicsAsync([new TopicSpecification { Name = topic, NumPartitions = 1, ReplicationFactor = 1 }]);
            topicCreated = true;
            using var onlineApi = CreateApi(dbSettings.ConnectionString, kafka, topic, actor);
            await Task.WhenAll(DispatchAsync(onlineApi), DispatchAsync(onlineApi));
            var sent = Assert.Single(await ReadOutboxAsync(db, postId));
            Assert.NotNull(sent.PublishedAt);
            Assert.Equal(2, sent.Attempts);

            using var searchClient = onlineApi.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
            using var search = await searchClient.PostAsJsonAsync(new Uri("/api/ai/highlight-rental-posts", UriKind.Relative),
                new { Text = "Tìm phòng dưới 2 triệu ở Linh Trung" });
            Assert.Equal(HttpStatusCode.OK, search.StatusCode);
            var searchResult = await search.Content.ReadFromJsonAsync<AiHighlightResponseDto>();
            Assert.NotNull(searchResult);
            Assert.Contains(searchResult.Posts, result => result.Post.Id == postId);

            string? publishedPayload = null;
            foreach (var group in new[] { $"observer-{runId}", $"search-{runId}" })
            {
                using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
                {
                    BootstrapServers = kafka, GroupId = group, AutoOffsetReset = AutoOffsetReset.Earliest,
                    AllowAutoCreateTopics = false
                }).Build();
                consumer.Subscribe(topic);
                var received = consumer.Consume(TimeSpan.FromSeconds(15));
                Assert.NotNull(received);
                Assert.Equal(postId.ToString(), received.Message.Key);
                using var envelope = JsonDocument.Parse(received.Message.Value);
                var fields = envelope.RootElement;
                Assert.Equal(publication.Id, fields.GetProperty("eventId").GetGuid());
                Assert.Equal(postId, fields.GetProperty("listingId").GetGuid());
                Assert.Equal("RentalListingPublished", fields.GetProperty("eventType").GetString());
                Assert.Equal(1, fields.GetProperty("schemaVersion").GetInt32());
                Assert.Equal(5, fields.EnumerateObject().Count());
                Assert.Null(consumer.Consume(TimeSpan.FromMilliseconds(300)));
                publishedPayload = received.Message.Value;
                consumer.Close();
            }

            var analyticsGroup = $"homeji-analytics-test-{runId}";
            using var producer = new ProducerBuilder<string, string>(new ProducerConfig { BootstrapServers = kafka }).Build();
            await producer.ProduceAsync(topic, new Message<string, string> { Key = postId.ToString(), Value = "{invalid-json" });
            var replay = await producer.ProduceAsync(topic, new Message<string, string> { Key = postId.ToString(), Value = publishedPayload! });
            using var analyticsApi = CreateApi(dbSettings.ConnectionString, kafka, topic, actor, analyticsGroup);
            using var analyticsClient = analyticsApi.CreateClient();
            using var offsetReader = new ConsumerBuilder<string, string>(new ConsumerConfig { BootstrapServers = kafka, GroupId = analyticsGroup }).Build();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (offsetReader.Committed([replay.TopicPartition], TimeSpan.FromSeconds(5)).Single().Offset.Value <= replay.Offset.Value)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), deadline.Token);
            }

            var fact = Assert.Single(await db.Set<RentalPublicationFact>().AsNoTracking().ToListAsync());
            Assert.Equal(publication.Id, fact.EventId);
            Assert.Equal(postId, fact.RentalPostId);
            using var originalEnvelope = JsonDocument.Parse(publishedPayload!);
            Assert.Equal(originalEnvelope.RootElement.GetProperty("occurredAt").GetDateTimeOffset(), fact.OccurredAt);
        }
        finally
        {
            await using var drop = new NpgsqlCommand($"DROP DATABASE {dbSettings.Database} WITH (FORCE)", parent);
            await drop.ExecuteNonQueryAsync();
            if (topicCreated)
            {
                await admin.DeleteTopicsAsync([topic]);
            }
        }
    }

    private static Task<List<OutboxRow>> ReadOutboxAsync(ApplicationDbContext db, Guid postId) =>
        db.Database.SqlQuery<OutboxRow>($"""
            SELECT id AS "Id", published_at AS "PublishedAt", attempts AS "Attempts"
            FROM homeji.rental_publications WHERE rental_post_id = {postId}
            """).ToListAsync();

    private static async Task DispatchAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRentalPublicationDispatcher>().DispatchBatchAsync(default);
    }

    private static WebApplicationFactory<Program> CreateApi(string connection, string kafka, string topic, Guid actor, string? analyticsGroup = null) =>
        new HomejiApiFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection);
            builder.UseSetting("BackgroundJobs:Enabled", analyticsGroup is null ? "false" : "true");
            builder.UseSetting("Database:ApplyMigrationsOnStartup", "false");
            builder.UseSetting("Kafka:RentalPublications:Enabled", "true");
            builder.UseSetting("Kafka:RentalPublications:BootstrapServers", kafka);
            builder.UseSetting("Kafka:RentalPublications:Topic", topic);
            if (analyticsGroup is not null)
            {
                builder.UseSetting("Kafka:RentalPublications:AnalyticsEnabled", "true");
                builder.UseSetting("Kafka:RentalPublications:AnalyticsGroupId", analyticsGroup);
            }
            builder.ConfigureTestServices(services =>
            {
                foreach (var worker in services.Where(descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                    descriptor.ImplementationType?.Namespace == "Homeji.Api.BackgroundJobs" &&
                    descriptor.ImplementationType.Name != "RentalPublicationAnalyticsWorker").ToArray())
                {
                    services.Remove(worker);
                }
                services.AddSingleton(new FixtureActor(actor));
                services.AddSingleton<IAiSearchTextParser, FixtureParser>();
                services.AddAuthentication("PublicationTest")
                    .AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("PublicationTest", _ => { });
            });
        });

    private static async Task<(Guid Actor, Guid RentalPostId)> SeedAsync(ApplicationDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("CREATE SCHEMA auth; CREATE TABLE auth.users(id uuid PRIMARY KEY, email text, email_confirmed_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(), deleted_at timestamptz);");
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var actor = Guid.NewGuid();
        var owner = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({actor}), ({owner})");
        var moderator = UserProfile.Create(actor, "Publication moderator", now);
        moderator.SetRole(UserRole.Admin, now);
        db.UserProfiles.AddRange(moderator, UserProfile.Create(owner, "Publication owner", now));

        var rental = RentalPost.CreateDraft(owner, RentalPostType.VacantRoom, now);
        rental.UpdateDetails(RentalPostType.VacantRoom, "Rental publication test", "Fixture description", 1500000,
            0, 20, "Linh Trung", 10.8m, 106.8m, [], now);
        for (var index = 0; index < RentalPost.MinimumImageCountForSubmit; index++)
        {
            rental.AddMedia(MediaType.Image, "rental-posts", $"{index}.jpg", index == 0, index, now);
        }
        rental.Submit(now);
        db.RentalPosts.Add(rental);
        await db.SaveChangesAsync();
        return (actor, rental.Id);
    }

    private sealed record OutboxRow(Guid Id, DateTimeOffset? PublishedAt, int Attempts);
    private sealed record FixtureActor(Guid Id);

    private sealed class FixtureParser : IAiSearchTextParser
    {
        public Task<AiParsedSearchCriteriaDto> ParseAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult(new AiParsedSearchCriteriaDto("Linh Trung", null, null, 2_000_000, null, null, []));
    }

    private sealed class FixtureAuthentication(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
        FixtureActor actor) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", actor.Id.ToString())], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}

internal sealed class RentalPublicationKafkaFactAttribute : FactAttribute
{
    public RentalPublicationKafkaFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOMEJI_TEST_KAFKA")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")))
        {
            Skip = "Set HOMEJI_TEST_KAFKA and HOMEJI_TEST_DATABASE to the disposable local services.";
        }
    }
}
