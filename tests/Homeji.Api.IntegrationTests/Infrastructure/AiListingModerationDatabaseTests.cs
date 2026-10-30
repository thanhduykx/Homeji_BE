using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Moderation;
using Homeji.Application.IServices.Moderation;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Homeji.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Api.IntegrationTests.Infrastructure;

public sealed class AiListingModerationDatabaseTests
{
    [LocalMarketplaceDatabaseFact]
    public async Task Durable_queue_serializes_workers_preserves_retries_and_rejects_stale_actions()
    {
        var original = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!);
        if (original.Host != "127.0.0.1" || original.Database != "homeji_quality") throw new InvalidOperationException("Isolated local database required.");
        var databaseName = "homeji_quality_moderation_" + Guid.NewGuid().ToString("N");
        var administrator = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = "postgres" };
        await using var adminConnection = new NpgsqlConnection(administrator.ConnectionString);
        await adminConnection.OpenAsync();
        await using (var create = new NpgsqlCommand("CREATE DATABASE \"" + databaseName + "\"", adminConnection)) await create.ExecuteNonQueryAsync();
        var connection = new NpgsqlConnectionStringBuilder(original.ConnectionString) { Database = databaseName }.ConnectionString;
        try { await VerifyAsync(connection); }
        finally {
            NpgsqlConnection.ClearAllPools();
            await using var drop = new NpgsqlCommand("DROP DATABASE \"" + databaseName + "\" WITH (FORCE)", adminConnection);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private static async Task VerifyAsync(string connection)
    {
        ApplicationDbContext Open() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
        await using (var setup = Open()) {
            await setup.Database.ExecuteSqlRawAsync("CREATE SCHEMA auth; CREATE TABLE auth.users(id uuid PRIMARY KEY, email text, email_confirmed_at timestamptz, created_at timestamptz NOT NULL DEFAULT now(), deleted_at timestamptz);");
            await setup.Database.MigrateAsync();
        }
        var clock = new Clock(DateTimeOffset.Parse("2030-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        var actor = Guid.NewGuid(); var seller = Guid.NewGuid();
        await using (var seed = Open()) {
            await seed.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({actor}), ({seller})");
            var profile = UserProfile.Create(actor, "Admin fixture", clock.Now); profile.SetRole(UserRole.Admin, clock.Now); seed.UserProfiles.Add(profile);
            seed.UserProfiles.Add(UserProfile.Create(seller, "Seller fixture", clock.Now)); await seed.SaveChangesAsync();
        }
        var ai = new Client();
        AiListingModeration Service(ApplicationDbContext db, Guid? id = null) => new(db, ai, new UserContext(new CurrentUser(id ?? actor), new UserProfileRepository(db)), clock);
        await using (var initialize = Open()) await Service(initialize).GetDashboardAsync(default);
        MarketplacePost food; MarketplacePost old;
        await using (var seed = Open()) {
            food = new MarketplacePost(seller, "Bữa cơm", "Cơm trưa", 30000, "Mới", "Đồ ăn", "Linh Trung", 10.85m, 106.78m, null, ["https://example.test/food.png"], clock.Now.AddSeconds(1), MarketplaceListingType.Food, 10);
            old = new MarketplacePost(seller, "Bàn học", "Bàn còn tốt", 100000, "Cũ", "Đồ dùng", "Linh Trung", 10.85m, 106.78m, null, ["https://example.test/goods.png"], clock.Now.AddDays(-1));
            seed.AddRange(food, old); await seed.SaveChangesAsync();
        }
        clock.Now = clock.Now.AddMinutes(1);
        ai.Hold();
        await using var first = Open();
        var running = Service(first).TickAsync(default); await ai.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var competing = Open()) await Service(competing).TickAsync(default);
        Assert.Equal(1, ai.Calls);
        ai.Release(new("clear", "none", "Chưa thấy dấu hiệu vi phạm.", "", .8)); await running;
        await using (var check = Open()) {
            Assert.False(await check.AiListingReviews.AnyAsync(j => j.SubjectId == old.Id));
            Assert.Equal("clear", (await check.AiListingReviews.SingleAsync(j => j.SubjectId == food.Id)).State);
        }
        clock.Now = clock.Now.AddMinutes(2);
        await using (var unchanged = Open()) await Service(unchanged).TickAsync(default);
        Assert.Equal(1, ai.Calls);
        await using (var edit = Open()) {
            var listing = await edit.MarketplacePosts.Include(p => p.Media).SingleAsync(p => p.Id == food.Id);
            listing.Update("Món mới", "bán ma túy", listing.Price, listing.Condition, listing.Category, listing.Address, listing.Latitude, listing.Longitude, null, ["https://example.test/food.png"], clock.Now, MarketplaceListingType.Food, 10);
            await edit.SaveChangesAsync();
        }
        ai.Error = new ExternalServiceUnavailableException("Gemini", "429", TimeSpan.FromMinutes(30));
        await using (var failed = Open()) await Service(failed).TickAsync(default);
        Assert.Equal(2, ai.Calls);
        clock.Now = clock.Now.AddMinutes(10);
        await using (var restart = Open()) await Service(restart).TickAsync(default);
        Assert.Equal(2, ai.Calls);
        clock.Now = clock.Now.AddMinutes(21); ai.Error = null; ai.Hold();
        await using var pendingDb = Open(); var pending = Service(pendingDb).TickAsync(default); await ai.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var edit = Open()) {
            var listing = await edit.MarketplacePosts.Include(p => p.Media).SingleAsync(p => p.Id == food.Id);
            listing.Update("Cơm trưa mới", "Món ăn cập nhật", listing.Price, listing.Condition, listing.Category, listing.Address, listing.Latitude, listing.Longitude, null, ["https://example.test/food.png"], clock.Now, MarketplaceListingType.Food, 10);
            await edit.SaveChangesAsync();
        }
        ai.Release(new("block", "illegal_drugs", "Tin kiểm thử hàng cấm", "bán ma túy", .99)); await pending;
        await using (var check = Open()) {
            Assert.True(await check.AiListingReviews.AnyAsync(j => j.SubjectId == food.Id && j.State == "superseded"));
            Assert.Equal(MarketplacePostStatus.Active, (await check.MarketplacePosts.SingleAsync(p => p.Id == food.Id)).Status);
        }
        // A fresh exact-evidence decision can hide a listing, and an admin can undo it.
        clock.Now = clock.Now.AddMinutes(11);
        await using (var edit = Open()) {
            var listing = await edit.MarketplacePosts.Include(p => p.Media).SingleAsync(p => p.Id == food.Id);
            listing.Update("Tin kiểm thử", "bán ma túy", listing.Price, listing.Condition, listing.Category, listing.Address, listing.Latitude, listing.Longitude, null, ["https://example.test/food.png"], clock.Now, MarketplaceListingType.Food, 10); await edit.SaveChangesAsync();
        }
        ai.Result = new("block", "illegal_drugs", "Tin kiểm thử hàng cấm", "bán ma túy", .99);
        await using (var block = Open()) await Service(block).TickAsync(default);
        Guid blockedId;
        await using (var check = Open()) {
            Assert.Equal(MarketplacePostStatus.Archived, (await check.MarketplacePosts.SingleAsync(p => p.Id == food.Id)).Status);
            blockedId = (await check.AiListingReviews.SingleAsync(j => j.SubjectId == food.Id && j.State == "blocked")).Id;
            Assert.Equal(0, await check.WalletTransactions.CountAsync()); Assert.Equal(0, await check.MarketplaceOrders.CountAsync());
        }
        await using (var nonAdmin = Open()) await Assert.ThrowsAsync<ForbiddenAccessException>(() => Service(nonAdmin, seller).GetDashboardAsync(default));
        await using (var restore = Open()) await Service(restore).ResolveAsync(blockedId, true, default);
        await using (var verify = Open()) {
            Assert.Equal(MarketplacePostStatus.Active, (await verify.MarketplacePosts.SingleAsync(p => p.Id == food.Id)).Status);
            var record = await verify.AiListingReviews.SingleAsync(j => j.Id == blockedId);
            Assert.Equal(actor, record.ReviewedBy); Assert.Equal("restored", record.State);
        }
        await using (var backfill = Open()) { await Service(backfill).QueueBackfillAsync(default); }
        clock.Now = clock.Now.AddMinutes(11); ai.Result = new("clear", "none", "Chưa thấy dấu hiệu vi phạm.", "", .8);
        await using (var backfill = Open()) await Service(backfill).TickAsync(default);
        await using (var check = Open()) Assert.True(await check.AiListingReviews.AnyAsync(j => j.SubjectId == old.Id));
    }
    private sealed record CurrentUser(Guid? UserId) : ICurrentUser;
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Client : IAiListingReviewClient
    {
        public int Calls { get; private set; }
        public Exception? Error { get; set; }
        public AiReviewVerdict Result { get; set; } = new("clear", "none", "Chưa thấy dấu hiệu vi phạm.", "", .8);
        private TaskCompletionSource<AiReviewVerdict>? _pending;
        public TaskCompletionSource Entered { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Hold() { _pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Entered = new(TaskCreationOptions.RunContinuationsAsynchronously); }
        public void Release(AiReviewVerdict result) { var pending = _pending; _pending = null; pending!.SetResult(result); }
        public Task<AiReviewVerdict> ReviewAsync(string snapshot, CancellationToken cancellationToken) { Calls++; Entered.TrySetResult(); if (Error is not null) return Task.FromException<AiReviewVerdict>(Error); return _pending?.Task ?? Task.FromResult(Result); }
    }
}
