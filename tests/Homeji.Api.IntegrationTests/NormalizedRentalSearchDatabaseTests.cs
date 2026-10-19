using System.Text;
using System.Text.Json;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.Services.AI;
using Homeji.Application.IServices.RentalPosts;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.Repositories;
using Homeji.Api.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit.Abstractions;

namespace Homeji.Api.IntegrationTests;

public sealed class NormalizedRentalSearchDatabaseTests(ITestOutputHelper output)
{
    [LocalMarketplaceDatabaseFact]
    public async Task Database_normalization_matches_invariant_search_including_decomposed_marks()
    {
        await using var db = Open();
        string[] corpus = ["PHÒNG TRỌ Thủ Đức, Đường ĐỖ XUÂN HỢP", "Bếp riêng – CỬA SỔ", "100% _ \\ giá tốt",
            "a\u0323\u0302 ĐẠI HỌC", "İ ẞ Å café Ελληνικά", "Phòng\ttrọ  không gộp khoảng trắng", "😀 𐐀", ""];
        foreach (var text in corpus.Concat(corpus.Select(text => text.Normalize(NormalizationForm.FormD))))
        {
            var actual = await db.Database.SqlQuery<string>($"SELECT homeji.normalize_rental_search({text}) AS \"Value\"").SingleAsync();
            Assert.Equal(RentalSearchIntent.Normalize(text), actual);
        }
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Text_filters_precede_pagination_and_generated_columns_follow_direct_updates()
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var owner = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({owner})");
        db.UserProfiles.Add(UserProfile.Create(owner, "Chủ tin kiểm thử", DateTimeOffset.UtcNow));
        var now = DateTimeOffset.UtcNow;
        var other = Create(owner, "Phòng Bình Thạnh", "Có bếp", now);
        var wanted = Create(owner, "Phòng ĐỖ XUÂN HỢP", "Bếp riêng 100% _", now.AddMinutes(-1));
        db.RentalPosts.AddRange(other, wanted);
        await db.SaveChangesAsync();
        var search = new RentalPostSearchDto(null, null, null, null, null, null, null, null, null,
            [], PageSize: 1, Ids: [other.Id, wanted.Id], NormalizedLocation: "do xuan hop", NormalizedKeyword: "bếp riêng");
        var repo = new RentalPostRepository(db);
        Assert.Equal(wanted.Id, Assert.Single(await repo.SearchActiveAsync(search)).Id);
        Assert.Empty(await repo.SearchActiveAsync(search with { Page = 2 }));
        Assert.Equal(wanted.Id, Assert.Single(await repo.SearchActiveAsync(search with { NormalizedKeyword = "100% _" })).Id);
        Assert.Empty(await repo.SearchActiveAsync(search with { NormalizedKeyword = "100% x" }));
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE homeji.rental_posts SET title = 'Phòng LINH TRUNG' WHERE id = {wanted.Id}");
        Assert.Empty(await repo.SearchActiveAsync(search));
        Assert.Equal(wanted.Id, Assert.Single(await repo.SearchActiveAsync(search with { NormalizedLocation = "linh trung" })).Id);
        await transaction.RollbackAsync();
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Selective_normalized_query_uses_trigram_index_without_forcing_the_planner()
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TEMP TABLE rental_search_plan_sample ON COMMIT DROP AS
            SELECT n AS id, CASE WHEN n % 1000 = 0 THEN 'Phòng Đỗ Xuân Hợp có bếp riêng ' || n
                ELSE 'Phòng trọ khu dân cư thông thường ' || n END AS title
            FROM generate_series(1, 30000) n;
            ALTER TABLE rental_search_plan_sample ADD COLUMN search_title text
                GENERATED ALWAYS AS (homeji.normalize_rental_search(title)) STORED;
            DO $ext$
            DECLARE extension_schema text;
            BEGIN
                SELECT n.nspname INTO STRICT extension_schema FROM pg_extension e
                JOIN pg_namespace n ON n.oid = e.extnamespace WHERE e.extname = 'pg_trgm';
                EXECUTE format('CREATE INDEX rental_sample_trgm ON rental_search_plan_sample USING gin (search_title %I.gin_trgm_ops)', extension_schema);
            END $ext$;
            ANALYZE rental_search_plan_sample;
            """);
        var legacy = await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM rental_search_plan_sample WHERE homeji.normalize_rental_search(title) LIKE '%do xuan hop%'").ToArrayAsync();
        var indexed = await db.Database.SqlQuery<int>($"SELECT id AS \"Value\" FROM rental_search_plan_sample WHERE search_title LIKE '%do xuan hop%'").ToArrayAsync();
        Assert.Equal(30, indexed.Length);
        Assert.Equal(legacy.Order(), indexed.Order());
        var baselinePlan = await ExplainAsync(db, "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT id FROM rental_search_plan_sample WHERE homeji.normalize_rental_search(title) LIKE '%do xuan hop%'");
        var indexPlan = await ExplainAsync(db, "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) SELECT id FROM rental_search_plan_sample WHERE search_title LIKE '%do xuan hop%'");
        output.WriteLine("Synthetic 30,000-row baseline plan: " + baselinePlan);
        output.WriteLine("Synthetic 30,000-row indexed plan: " + indexPlan);
        Assert.Contains("rental_sample_trgm", indexPlan, StringComparison.Ordinal);
        await transaction.RollbackAsync();
    }

    [LocalMarketplaceDatabaseFact]
    public async Task Public_rental_page_excludes_private_states_and_never_counts_crawler_views()
    {
        await using var db = Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var owner = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO auth.users(id) VALUES ({owner})");
        db.UserProfiles.Add(UserProfile.Create(owner, "Chủ tin riêng tư", DateTimeOffset.UtcNow));
        var now = DateTimeOffset.UtcNow;
        var active = Create(owner, "Tin công khai", "Mô tả công khai", now);
        var archived = Create(owner, "Tin đã ẩn", "Nội dung riêng", now);
        archived.Archive(now);
        var draft = RentalPost.CreateDraft(owner, RentalPostType.VacantRoom, now);
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var transfer = CreateTransfer(owner, today.AddDays(10), now);
        var expired = CreateTransfer(owner, today.AddDays(-1), now);
        var synthetic = Create(owner, "Tin tổng hợp", "Không công khai", now);
        db.Entry(synthetic).Property(post => post.IsSynthetic).CurrentValue = true;
        db.RentalPosts.AddRange(active, archived, draft, transfer, expired, synthetic);
        await db.SaveChangesAsync();
        var controller = new PublicRentalsController(new PublicRentalCatalog(db, TimeProvider.System));
        var result = Assert.IsType<OkObjectResult>((await controller.Get(active.Id, CancellationToken.None)).Result);
        var page = Assert.IsType<PublicRentalPage>(result.Value);
        Assert.Equal(active.Id, page.Id);
        Assert.IsType<NotFoundResult>((await controller.Get(archived.Id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Get(draft.Id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Get(Guid.NewGuid(), CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Get(expired.Id, CancellationToken.None)).Result);
        Assert.IsType<NotFoundResult>((await controller.Get(synthetic.Id, CancellationToken.None)).Result);
        var transferPage = Assert.IsType<PublicRentalPage>(Assert.IsType<OkObjectResult>(
            (await controller.Get(transfer.Id, CancellationToken.None)).Result).Value);
        Assert.Equal("Khu vực Thủ Đức, TP Hồ Chí Minh", transferPage.Address);
        var serialized = JsonSerializer.Serialize(transferPage);
        Assert.DoesNotContain("123 Đường riêng tư", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-owner-contact@example.test", serialized, StringComparison.Ordinal);
        Assert.Equal(0, await db.RentalPosts.Where(post => post.Id == active.Id).Select(post => post.ViewCount).SingleAsync());
        var sitemap = Assert.IsType<PublicRentalSitemap>(Assert.IsType<OkObjectResult>((await controller.Sitemap()).Result).Value);
        Assert.Contains(sitemap.Items, item => item.Id == active.Id);
        Assert.DoesNotContain(sitemap.Items, item => item.Id == archived.Id || item.Id == draft.Id);
        Assert.Contains(sitemap.Items, item => item.Id == transfer.Id);
        Assert.DoesNotContain(sitemap.Items, item => item.Id == expired.Id || item.Id == synthetic.Id);
        await transaction.RollbackAsync();
    }

    private static async Task<string> ExplainAsync(ApplicationDbContext db, string sql)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = sql;
        return (string)(await command.ExecuteScalarAsync())!;
    }

    private static RentalPost Create(Guid owner, string title, string description, DateTimeOffset now)
    {
        var post = RentalPost.CreateDraft(owner, RentalPostType.VacantRoom, now);
        post.UpdateDetails(RentalPostType.VacantRoom, title, description, 3_000_000, 0, 25, "Thủ Đức", 10.85m, 106.8m, [], now);
        for (var index = 0; index < 3; index++) post.AddMedia(MediaType.Image, "test", $"{post.Id:N}-{index}.jpg", index == 0, index, now);
        post.Submit(now);
        post.Approve(now);
        return post;
    }

    private static RentalPost CreateTransfer(Guid owner, DateOnly leaseEnd, DateTimeOffset now)
    {
        var post = RentalPost.CreateDraft(owner, RentalPostType.RoomTransfer, now);
        post.UpdateDetails(RentalPostType.RoomTransfer, "Tin chuyển nhượng", "Mô tả công khai", 3_000_000, 0, 25,
            "123 Đường riêng tư, Thủ Đức, TP Hồ Chí Minh", 10.85m, 106.8m, [], now,
            availableFrom: leaseEnd.AddDays(-30), transferKind: RoomTransferKind.LeaseAssignment,
            originalLeaseEndsOn: leaseEnd, transferReason: "Chuyển nơi làm việc", ownerConsentConfirmed: true,
            ownerConsentContact: "private-owner-contact@example.test");
        for (var index = 0; index < 3; index++) post.AddMedia(MediaType.Image, "test", $"{post.Id:N}-{index}.jpg", index == 0, index, now);
        post.Submit(now);
        post.Approve(now, owner, "Đã kiểm tra đồng ý");
        return post;
    }

    private static ApplicationDbContext Open()
    {
        var connection = Environment.GetEnvironmentVariable("HOMEJI_TEST_DATABASE")!;
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host != "127.0.0.1" || settings.Database != "homeji_quality")
            throw new InvalidOperationException("Only the disposable loopback homeji_quality database is allowed.");
        return new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options);
    }
}
