using System.Net;
using System.Text.Json;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.AI;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.IServices.AI;
using Homeji.Application.IServices.Emails;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Api.IntegrationTests;

public sealed class SearchInterestDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Comparison_entitlement_checks_active_subscription_and_reverts_after_expiry()
    {
        await using var db = Open(); var user = await Seed(db); var clock = new Clock();
        var service = new Homeji.Application.Services.AI.RentalDecisionService(new RentalPostRepository(db, clock),
            Context(user), null!, null!, clock, null!, new UserSubscriptionRepository(db));
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CompareAsync(new(ids), default));
        db.UserSubscriptions.Add(UserSubscription.CreatePremium(user, "TEST", "Premium", null, clock.At, 1, clock.At));
        await db.SaveChangesAsync();
        Assert.Equal(5, (await service.CompareAsync(new(ids), default)).UnavailablePostIds.Count);
        clock.At = clock.At.AddDays(2);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.CompareAsync(new(ids), default));
        Assert.Equal(3, (await service.CompareAsync(new(ids.Take(3).ToArray()), default)).UnavailablePostIds.Count);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Premium_extends_follow_limit_and_expiry_preserves_saved_areas_without_allowing_new_ones()
    {
        await using var db = Open(); var user = await Seed(db); var clock = new Clock(); var service = Service(db, user, clock);
        await service.SettingsAsync(new(true), default);
        for (var index = 0; index < 10; index++) await service.FollowAsync(new($"Khu kiểm thử {index}"), default);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.FollowAsync(new("Khu thứ 11"), default));
        db.UserSubscriptions.Add(UserSubscription.CreatePremium(user, "TEST", "Premium", null, clock.At, 1, clock.At));
        await db.SaveChangesAsync();
        for (var index = 10; index < 30; index++) await service.FollowAsync(new($"Khu kiểm thử {index}"), default);
        Assert.Equal(30, (await service.GetAsync(default)).MaxFollowedAreas);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.FollowAsync(new("Khu thứ 31"), default));
        clock.At = clock.At.AddDays(2);
        var expired = await service.GetAsync(default);
        Assert.Equal(10, expired.MaxFollowedAreas);
        Assert.Equal(30, expired.Following.Count);
        Assert.Contains("vẫn được lưu", expired.Notice, StringComparison.Ordinal);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.FollowAsync(new("Khu mới sau hết hạn"), default));
        await service.RemoveAsync(expired.Following[0].Id, default);
        Assert.Equal(29, (await service.GetAsync(default)).Following.Count);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Automatic_defaults_enable_tracking_and_ai_once_without_enabling_email_or_overriding_later_choices()
    {
        await using var db = Open();
        var user = await Seed(db);
        var legacyUser = await Seed(db);
        var clock = new Clock();
        db.Set<SearchPersonalization>().Add(new SearchPersonalization
        {
            UserId = legacyUser, CheckedAt = clock.At, Enabled = false, AiEnabled = false, EmailEnabled = false,
        });
        await db.SaveChangesAsync();
        foreach (var owner in new[] { user, legacyUser })
        {
            var service = Service(db, owner, clock);
            var initial = await service.GetAsync(default);
            Assert.True(initial.Settings.Enabled);
            Assert.True(initial.Settings.AiEnabled);
            Assert.False(initial.Settings.EmailEnabled);
            await service.SettingsAsync(new(false, false, false), default);
            var disabled = await Service(db, owner, clock).GetAsync(default);
            Assert.False(disabled.Settings.Enabled);
            Assert.False(disabled.Settings.AiEnabled);
            await service.SettingsAsync(new(true, false, false), default);
            var aiDisabled = await Service(db, owner, clock).GetAsync(default);
            Assert.True(aiDisabled.Settings.Enabled);
            Assert.False(aiDisabled.Settings.AiEnabled);
        }
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Honors_disabled_settings_three_distinct_sessions_dismissal_retention_and_owner()
    {
        await using var db = Open(); var user = await Seed(db); var other = await Seed(db); var clock = new Clock();
        await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(db, Guid.NewGuid(), clock).GetAsync(default));
        var service = Service(db, user, clock); var criteria = new InterestCriteria("Thủ Đức", MaxPrice: 4000000);
        await service.SettingsAsync(new(false), default);
        var initial = await service.ObserveAsync(new(Guid.NewGuid(), criteria), default);
        Assert.False(initial.Settings.Enabled); Assert.Empty(initial.Suggestions);
        await service.SettingsAsync(new(true), default);
        var session = Guid.NewGuid();
        for (var i = 0; i < 4; i++) await service.ObserveAsync(new(session, criteria), default);
        Assert.Empty((await service.GetAsync(default)).Suggestions);
        await service.ObserveAsync(new(Guid.NewGuid(), criteria with { Area = "THU DUC" }), default);
        var third = await service.ObserveAsync(new(Guid.NewGuid(), criteria), default);
        Assert.Single(third.Suggestions); Assert.Equal(3, third.Suggestions[0].Sessions);
        Assert.Empty((await Service(db, other, clock).GetAsync(default)).Suggestions);
        Assert.Empty((await service.DismissAsync("thu duc", default)).Suggestions);
        clock.At = clock.At.AddDays(8);
        Assert.Empty((await service.GetAsync(default)).Suggestions);
        await service.FollowAsync(criteria, default);
        var disabled = await service.SettingsAsync(new(false), default);
        Assert.Empty(disabled.Following); Assert.Empty(disabled.Suggestions);
        var stored = await db.Set<SearchPersonalization>().SingleAsync(x => x.UserId == user);
        Assert.DoesNotContain("Thủ Đức", stored.Data, StringComparison.Ordinal);
        var legacy = await new UserProfileRepository(db).GetMatchingRentersAsync("Thủ Đức", 3000000, other, 500);
        Assert.DoesNotContain(legacy, p => p.Id == user);
        var protectedTable = await db.Database.SqlQueryRaw<bool>("""
            SELECT c.relrowsecurity AND NOT has_table_privilege('authenticated','homeji.search_personalization','SELECT')
                AND NOT has_table_privilege('anon','homeji.search_personalization','SELECT') AS "Value"
            FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
            WHERE n.nspname='homeji' AND c.relname='search_personalization'
            """).SingleAsync();
        Assert.True(protectedTable);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task New_rooms_enforce_price_amenities_radius_availability_and_seen_filters()
    {
        await using var db = Open(); var user = await Seed(db); var owner = await Seed(db); var clock = new Clock(); var service = Service(db, user, clock);
        await service.SettingsAsync(new(true), default);
        await service.FollowAsync(new("Khu vực kiểm thử", MaxPrice: 4000000, Amenities: ["WIFI"], Latitude: 15.123, Longitude: 108.543, RadiusKm: 2), default);
        clock.At = clock.At.AddSeconds(2);
        var good = AddPost(db, owner, clock.At, 3000000, 15.123m, 108.543m);
        AddPost(db, owner, clock.At, 5000000, 15.123m, 108.543m);
        AddPost(db, owner, clock.At, 3000000, 15.223m, 108.543m);
        AddPost(db, owner, clock.At, 3000000, 15.123m, 108.543m, wifi: false);
        AddPost(db, owner, clock.At, 3000000, 15.123m, 108.543m, active: false);
        AddPost(db, owner, clock.At.AddDays(-1), 3000000, 15.123m, 108.543m);
        AddPost(db, user, clock.At, 3000000, 15.123m, 108.543m);
        await db.SaveChangesAsync();
        var overview = await service.GetAsync(default);
        Assert.Equal(good.Id, Assert.Single(overview.Rooms).Post.Id);
        await service.ViewedAsync(good.Id, default);
        Assert.Empty((await service.GetAsync(default)).Rooms);
        await service.ClearAsync(default);
        Assert.Single((await service.GetAsync(default)).Following);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task In_app_notifications_are_deduplicated_and_email_requires_opt_in_and_is_daily()
    {
        await using var db = Open(); var user = await Seed(db); var owner = await Seed(db); var clock = new Clock(); var sender = new Sender(); var area = "Linh Trung " + Guid.NewGuid().ToString("N"); var service = Service(db, user, clock, sender);
        await service.SettingsAsync(new(true), default);
        await service.FollowAsync(new(area, MaxPrice: 4000000), default);
        clock.At = clock.At.AddMinutes(20); AddPost(db, owner, clock.At, 3000000, 10.8m, 106.7m, location: area); await db.SaveChangesAsync();
        await service.ProcessBatchAsync(default);
        Assert.Single(await db.Notifications.Where(x => x.RecipientId == user && x.Type == NotificationType.NewMatchingRentalPost).ToListAsync());
        Assert.Empty(sender.Emails);
        await service.SettingsAsync(new(true, false, true), default);
        clock.At = clock.At.AddMinutes(20); await service.ProcessBatchAsync(default);
        Assert.Single(sender.Emails, email => email == user.ToString("N") + "@example.test");
        AddPost(db, owner, clock.At.AddSeconds(1), 3000000, 10.8m, 106.7m, location: area); await db.SaveChangesAsync();
        clock.At = clock.At.AddMinutes(20); await service.ProcessBatchAsync(default);
        Assert.Single(sender.Emails, email => email == user.ToString("N") + "@example.test");
        clock.At = clock.At.AddHours(25); await service.ProcessBatchAsync(default);
        Assert.Equal(2, sender.Emails.Count(email => email == user.ToString("N") + "@example.test"));
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.RecipientId == user && x.Type == NotificationType.NewMatchingRentalPost));
        await service.SettingsAsync(new(false), default);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task AI_proposes_only_after_consent_preserves_explicit_budget_and_never_follows_automatically()
    {
        await using var db = Open(); var user = await Seed(db); var clock = new Clock(); var parser = new Parser(); var service = Service(db, user, clock, parser: parser);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.InferAsync(default));
        await service.SettingsAsync(new(true, true), default);
        var criteria = new InterestCriteria("Đại học FPT", MaxPrice: 4000000);
        var sameSession = Guid.NewGuid();
        await service.ObserveAsync(new(sameSession, criteria), default);
        await service.ObserveAsync(new(sameSession, criteria with { Area = "Thủ Đức" }), default);
        await service.ObserveAsync(new(sameSession, criteria with { Area = "Linh Trung" }), default);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.InferAsync(default));
        for (var i = 0; i < 3; i++) await service.ObserveAsync(new(Guid.NewGuid(), criteria), default);
        var result = await service.InferAsync(default);
        Assert.Equal(4000000, result.Criteria!.MaxPrice);
        Assert.Empty((await service.GetAsync(default)).Following);
        Assert.Single(parser.Texts); Assert.DoesNotContain(user.ToString(), parser.Texts[0], StringComparison.Ordinal);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.InferAsync(default));
        await service.ClearAsync(default);
        for (var i = 0; i < 3; i++) await service.ObserveAsync(new(Guid.NewGuid(), criteria), default);
        await Assert.ThrowsAsync<RequestValidationException>(() => service.InferAsync(default));
        await service.SettingsAsync(new(false), default);
    }

    [LocalMarketplaceDatabaseFact]
    public async Task AI_failure_returns_normal_filters_and_ranking_discards_invented_IDs()
    {
        await using var db = Open(); var user = await Seed(db); var owner = await Seed(db); var clock = new Clock(); var parser = new Parser { Fail = true }; var area = "Linh Trung " + Guid.NewGuid().ToString("N");
        var service = Service(db, user, clock, parser: parser);
        await service.SettingsAsync(new(true, true), default);
        for (var i = 0; i < 3; i++) await service.ObserveAsync(new(Guid.NewGuid(), new(area)), default);
        Assert.Null((await service.InferAsync(default)).Criteria);
        await service.FollowAsync(new(area, MaxPrice: 4000000), default);
        clock.At = clock.At.AddSeconds(2); var post = AddPost(db, owner, clock.At, 3000000, 10.8m, 106.7m, location: area); await db.SaveChangesAsync();
        var response = "{\"ids\":[\"" + Guid.NewGuid() + "\",\"" + post.Id + "\",\"" + post.Id + "\"]}";
        var ranker = new GeminiInterestRanker(new HttpClient(new RankHandler(response)), Options.Create(new GeminiOptions { ApiKey = "fixture", Endpoint = "https://fixture.test/generate" }));
        var rankedService = new SearchInterestService(db, Context(user), parser, new Sender(), clock, ranker);
        var ranked = await rankedService.RankAsync(default);
        Assert.Equal(post.Id, Assert.Single(ranked.Rooms).Post.Id);
        await service.SettingsAsync(new(false), default);
    }

    [Fact]
    public void Invalid_criteria_do_not_silently_weaken_radius_or_price_limits()
    {
        Assert.Throws<RequestValidationException>(() => InterestPolicy.Validate(new("Thủ Đức", MinPrice: 5000000, MaxPrice: 4000000)));
        Assert.Throws<RequestValidationException>(() => InterestPolicy.Validate(new("Thủ Đức", RadiusKm: 3)));
        Assert.Throws<RequestValidationException>(() => InterestPolicy.Validate(new("Thủ Đức", Amenities: ["wifi"], ExcludedAmenities: [" WIFI "])));
        Assert.Throws<RequestValidationException>(() => InterestPolicy.Validate(new("Thủ Đức", Latitude: double.NaN, Longitude: 106.7, RadiusKm: 3)));
        Assert.Equal("thu duc", InterestPolicy.Key("  THỦ  ĐỨC "));
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality") throw new InvalidOperationException("Disposable local database required.");
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection, x => x.EnableRetryOnFailure()).Options);
    }
    private static async Task<Guid> Seed(ApplicationDbContext db)
    {
        var id = Guid.NewGuid(); var email = id.ToString("N") + "@example.test";
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id,email,email_confirmed_at) VALUES ({id},{email},{DateTimeOffset.UtcNow})");
        db.UserProfiles.Add(UserProfile.Create(id, "Interest test", DateTimeOffset.UtcNow)); await db.SaveChangesAsync(); return id;
    }
    private static RentalPost AddPost(ApplicationDbContext db, Guid owner, DateTimeOffset at, decimal price, decimal lat, decimal lng, bool wifi = true, bool active = true, string location = "Linh Trung")
    {
        var post = RentalPost.CreateDraft(owner, RentalPostType.VacantRoom, at);
        post.UpdateDetails(RentalPostType.VacantRoom, "Phòng mới kiểm thử", "Phòng thử đủ điều kiện", price, 1000000, 25, location, lat, lng, wifi ? ["WIFI"] : [], at);
        for (var i = 0; i < 3; i++) post.AddMedia(MediaType.Image, "homeji-media", $"{owner}/{post.Id}/interest-{i}.jpg", i == 0, i, at);
        if (active) { post.Submit(at); post.Approve(at); }
        db.RentalPosts.Add(post); return post;
    }
    private static UserContext Context(Guid user) => new(new CurrentUser(user), null!);
    private static SearchInterestService Service(ApplicationDbContext db, Guid user, Clock clock, Sender? sender = null, Parser? parser = null)
        => new(db, Context(user), parser ?? new Parser(), sender ?? new Sender(), clock, new GeminiInterestRanker(new HttpClient(), Options.Create(new GeminiOptions())));
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Clock : TimeProvider { public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow.AddHours(2); public override DateTimeOffset GetUtcNow() => At; }
    private sealed class Parser : IAiSearchTextParser
    {
        public bool Fail { get; set; }
        public List<string> Texts { get; } = [];
        public Task<AiParsedSearchCriteriaDto> ParseAsync(string text, CancellationToken cancellationToken = default)
        { Texts.Add(text); if (Fail) throw new HttpRequestException("Fixture provider unavailable"); return Task.FromResult(new AiParsedSearchCriteriaDto("Long Thạnh Mỹ", null, null, 6000000, null, null, [])); }
    }
    private sealed class Sender : ITransactionalEmailSender
    {
        public List<string> Emails { get; } = [];
        public Task<EmailSendResultDto> SendAsync(string email, string subject, string html, string text, Guid idempotencyKey, CancellationToken cancellationToken, IReadOnlyList<EmailAttachmentDto>? attachments = null)
        { Emails.Add(email); return Task.FromResult(new EmailSendResultDto(true, "fixture", false)); }
    }
    private sealed class RankHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = response } } } } } })) });
    }
}
