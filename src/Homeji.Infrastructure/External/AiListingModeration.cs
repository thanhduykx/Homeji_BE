using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Moderation;
using Homeji.Application.IServices.Moderation;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Moderation;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace Homeji.Infrastructure.External;

public sealed class AiListingModeration(ApplicationDbContext db, IAiListingReviewClient ai, UserContext userContext, TimeProvider clock) : IAiListingModeration
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private sealed record Claim(Guid Id, Guid Lease, string Snapshot);

    public async Task TickAsync(CancellationToken cancellationToken)
    {
        var claim = await LockedAsync<Claim?>(async () => {
            var now = clock.GetUtcNow();
            var schedule = await ScheduleAsync(cancellationToken);
            if (!schedule.Enabled || now < schedule.NextAllowedAt) return null;
            await DiscoverAsync(schedule, cancellationToken);
            if (await db.AiListingReviews.AnyAsync(j => j.State == "processing" && j.LeaseUntil > now, cancellationToken)) return null;
            var job = await db.AiListingReviews.Where(j => j.State == "pending" && j.NextAttemptAt <= now || j.State == "processing" && j.LeaseUntil <= now)
                .OrderBy(j => j.SourceUpdatedAt < schedule.StartedAt).ThenBy(j => j.CreatedAt).ThenBy(j => j.Id).FirstOrDefaultAsync(cancellationToken);
            if (job is null) return null;
            if (job.Attempts >= 3) { job.Retry(now, "Đã thử ba lần; cần quản trị viên kiểm tra."); return null; }
            var source = await SourceAsync(job, cancellationToken);
            if (source is null || Hash(source.Value.Json) != job.ContentHash
                || source.Value.Rental is { Status: not (RentalPostStatus.Active or RentalPostStatus.Pending) }
                || source.Value.Market is { Status: not MarketplacePostStatus.Active })
            { job.Complete("superseded", "none", "Tin đã đổi hoặc ngừng hiển thị, không cần gọi AI cho phiên bản cũ.", "", 0, now); return null; }
            // UTF-8 bytes conservatively bound input token use; reserve the full output allowance, never refund uncertain requests.
            var tokenReservation = Encoding.UTF8.GetByteCount(GeminiListingReviewClient.Instructions + job.Snapshot) + GeminiListingReviewClient.MaxOutputTokens;
            if (tokenReservation > schedule.DailyTokenBudget)
            { job.Complete("needs_review", "other", "Tin dài hơn ngân sách AI đã đặt. Cần người duyệt kiểm tra hoặc tăng ngân sách.", "", 0, now); return null; }
            if (!schedule.Reserve(tokenReservation, now)) return null;
            return new Claim(job.Id, job.Claim(now), job.Snapshot);
        }, cancellationToken);
        if (claim is null) return;
        AiReviewVerdict verdict;
        try { verdict = await ai.ReviewAsync(claim.Snapshot, cancellationToken); }
        catch (Exception error) when (error is ExternalServiceUnavailableException or HttpRequestException or OperationCanceledException)
        {
            // Leave the committed lease for recovery if the process itself was stopped.
            if (cancellationToken.IsCancellationRequested) throw;
            await LockedAsync(async () => {
                var job = await db.AiListingReviews.SingleOrDefaultAsync(j => j.Id == claim.Id && j.LeaseId == claim.Lease, cancellationToken);
                if (job is null) return false;
                var now = clock.GetUtcNow();
                var delay = error is ExternalServiceUnavailableException unavailable ? unavailable.RetryAfter ?? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(5);
                var next = now.Add(delay).AddSeconds(Random.Shared.Next(5, 31));
                job.Retry(next, error is ExternalServiceUnavailableException known ? known.Message : "Chưa kết nối được AI. Hệ thống sẽ kiểm tra lại sau.");
                (await ScheduleAsync(cancellationToken)).Backoff(next, job.Reason!);
                return true;
            }, cancellationToken);
            return;
        }
        await LockedAsync(async () => {
            var job = await db.AiListingReviews.SingleOrDefaultAsync(j => j.Id == claim.Id && j.LeaseId == claim.Lease, cancellationToken);
            if (job is null) return false;
            var now = clock.GetUtcNow();
            var source = await SourceAsync(job, cancellationToken);
            if (source is null || Hash(source.Value.Json) != job.ContentHash)
            { job.Complete("superseded", "none", "Nội dung đã thay đổi; kết quả cũ không được áp dụng.", "", 0, now); return true; }
            var schedule = await ScheduleAsync(cancellationToken);
            var autoBlock = schedule.Enabled && schedule.AutoBlock && AiListingReviewPolicy.CanAutoBlock(verdict, job.Snapshot);
            var state = verdict.Decision == "clear" ? "clear" : "needs_review";
            if (autoBlock)
            {
                var reason = "AI phát hiện nội dung cần ẩn: " + verdict.Reason;
                reason = reason[..Math.Min(reason.Length, 400)];
                if (source.Value.Rental is { } rental && rental.Status is RentalPostStatus.Pending or RentalPostStatus.Active)
                {
                    job.RecordBlock((int)rental.Status, now); rental.HideForAiReview(reason, now); state = "blocked";
                    db.Notifications.Add(new Notification(rental.OwnerId, NotificationType.PostRejected, "Tin đăng cần chỉnh sửa", reason + " Bạn có thể sửa tin hoặc liên hệ quản trị viên để xem lại.", rental.Id, now));
                }
                else if (source.Value.Market is { Status: MarketplacePostStatus.Active, ReservedQuantity: 0 } market)
                {
                    job.RecordBlock((int)market.Status, now); market.Archive(now); state = "blocked";
                    db.Notifications.Add(new Notification(market.SellerId, NotificationType.MarketplaceOrderUpdated, "Tin bán hàng đã được ẩn để kiểm tra", reason, market.Id, now));
                }
            }
            job.Complete(state, verdict.Category, verdict.Reason, verdict.Evidence, verdict.Confidence, now);
            return true;
        }, cancellationToken);
    }

    public async Task<AiReviewDashboardDto> GetDashboardAsync(CancellationToken cancellationToken)
    {
        await AdminAsync(cancellationToken);
        return await LockedAsync(async () => {
            var s = await ScheduleAsync(cancellationToken); s.ResetDay(clock.GetUtcNow());
            var jobs = await db.AiListingReviews.AsNoTracking().OrderByDescending(j => j.ReviewedAt == null && (j.State == "blocked" || j.State == "needs_review"))
                .ThenByDescending(j => j.CreatedAt).Take(100).ToListAsync(cancellationToken);
            var items = jobs.Select(j => {
                using var snapshot = JsonDocument.Parse(j.Snapshot);
                return new AiReviewItemDto(j.Id, j.SubjectId, j.Kind, j.Title, j.State, j.Category, j.Reason, j.Evidence, j.Confidence, j.Attempts, j.CreatedAt, j.CompletedAt, j.ReviewedAt,
                    snapshot.RootElement.GetProperty("description").GetString() ?? "");
            }).ToList();
            return new AiReviewDashboardDto(new(s.Enabled, s.AutoBlock, s.BatchSize, s.SpacingSeconds, s.PauseMinutes, s.DailyRequestLimit, s.DailyTokenBudget),
                s.NextAllowedAt, s.RequestsToday, s.TokensReservedToday, s.PauseReason,
                await db.AiListingReviews.CountAsync(j => j.State == "pending" || j.State == "processing", cancellationToken),
                await db.AiListingReviews.CountAsync(j => j.State == "needs_review" && j.ReviewedAt == null, cancellationToken),
                await db.AiListingReviews.CountAsync(j => j.State == "blocked" && j.ReviewedAt == null, cancellationToken), items);
        }, cancellationToken);
    }
    public async Task UpdateSettingsAsync(AiReviewSettingsDto settings, CancellationToken cancellationToken)
    {
        await AdminAsync(cancellationToken);
        if (settings.BatchSize is < 1 or > 10 || settings.SpacingSeconds is < 30 or > 3600 || settings.PauseMinutes is < 5 or > 1440
            || settings.DailyRequestLimit is < 1 or > 500 || settings.DailyTokenBudget is < 10000 or > 1000000)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["settings"] = ["Mỗi đợt 1–10 tin; cách nhau 30–3600 giây; nghỉ 5–1440 phút; 1–500 lượt và 10.000–1.000.000 token/ngày."] });
        await LockedAsync(async () => {
            var s = await ScheduleAsync(cancellationToken);
            s.Enabled = settings.Enabled; s.AutoBlock = settings.AutoBlock; s.BatchSize = settings.BatchSize; s.SpacingSeconds = settings.SpacingSeconds;
            s.PauseMinutes = settings.PauseMinutes; s.DailyRequestLimit = settings.DailyRequestLimit; s.DailyTokenBudget = settings.DailyTokenBudget;
            return true;
        }, cancellationToken);
    }
    public async Task QueueBackfillAsync(CancellationToken cancellationToken)
    {
        await AdminAsync(cancellationToken);
        await LockedAsync(async () => { (await ScheduleAsync(cancellationToken)).BackfillBefore = clock.GetUtcNow(); return true; }, cancellationToken);
    }
    public async Task ResolveAsync(Guid reviewId, bool restore, CancellationToken cancellationToken)
    {
        var admin = await AdminAsync(cancellationToken);
        await LockedAsync(async () => {
            var job = await db.AiListingReviews.SingleOrDefaultAsync(j => j.Id == reviewId, cancellationToken) ?? throw new NotFoundException(nameof(AiListingReview), reviewId);
            if (!restore && job.State == "needs_review")
            {
                var source = await SourceAsync(job, cancellationToken);
                if (source is null || Hash(source.Value.Json) != job.ContentHash)
                    throw new RequestValidationException(new Dictionary<string, string[]> { ["review"] = ["Nội dung tin đã đổi. Hãy kiểm tra phiên bản mới trước khi ẩn."] });
                var now = clock.GetUtcNow();
                if (source.Value.Rental is { } rental && rental.Status is RentalPostStatus.Active or RentalPostStatus.Pending)
                { job.RecordBlock((int)rental.Status, now); rental.HideForAiReview(job.Reason ?? "Quản trị viên yêu cầu kiểm tra lại nội dung.", now); }
                else if (source.Value.Market is { Status: MarketplacePostStatus.Active, ReservedQuantity: 0 } market)
                { job.RecordBlock((int)market.Status, now); market.Archive(now); }
                else throw new RequestValidationException(new Dictionary<string, string[]> { ["review"] = ["Tin đã đổi trạng thái hoặc đang có đơn xử lý. Hãy kiểm tra đơn trước khi ẩn tin."] });
            }
            if (restore && job.State == "blocked")
            {
                var source = await SourceAsync(job, cancellationToken);
                if (source is null || Hash(source.Value.Json) != job.ContentHash || source.Value.UpdatedAt != job.BlockedAt)
                    throw new RequestValidationException(new Dictionary<string, string[]> { ["review"] = ["Tin đã thay đổi sau lần kiểm tra. Hãy mở tin để kiểm tra phiên bản mới."] });
                if (source.Value.Rental is { } rental) rental.RestoreAfterAiReview(job.PreviousStatus!.Value, clock.GetUtcNow());
                else source.Value.Market!.RestoreAfterAiReview(clock.GetUtcNow());
            }
            job.Resolve(admin.Id, restore, clock.GetUtcNow()); return true;
        }, cancellationToken);
    }

    private async Task<UserProfile> AdminAsync(CancellationToken ct)
    { var admin = await userContext.GetRequiredProfileAsync(ct); UserContext.EnsureAdmin(admin); return admin; }
    private async Task<AiModerationSchedule> ScheduleAsync(CancellationToken ct)
    {
        var s = await db.AiModerationSchedules.SingleOrDefaultAsync(ct);
        if (s is not null) return s;
        s = new AiModerationSchedule { StartedAt = clock.GetUtcNow(), NextAllowedAt = clock.GetUtcNow() }; db.Add(s); return s;
    }
    private async Task<T> LockedAsync<T>(Func<Task<T>> action, CancellationToken ct) => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
        db.ChangeTracker.Clear(); await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(172932, 73)", ct);
        var result = await action(); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return result;
    });

    private async Task DiscoverAsync(AiModerationSchedule schedule, CancellationToken ct)
    {
        var rentals = await db.RentalPosts.Where(p => (p.Status == RentalPostStatus.Active || p.Status == RentalPostStatus.Pending)
            && (p.UpdatedAt >= schedule.StartedAt || schedule.BackfillBefore != null && p.CreatedAt <= schedule.BackfillBefore)
            && !db.AiListingReviews.Any(j => j.Kind == "rental" && j.SubjectId == p.Id && j.SourceUpdatedAt >= p.UpdatedAt))
            .OrderByDescending(p => p.UpdatedAt).Take(3).ToListAsync(ct);
        foreach (var rental in rentals) await EnqueueAsync(rental.Id, "rental", rental.Title, Snapshot(rental), rental.UpdatedAt, ct);
        foreach (var kind in new[] { MarketplaceListingType.SecondHand, MarketplaceListingType.Food })
        {
            var name = kind == MarketplaceListingType.Food ? "food" : "goods";
            var markets = await db.MarketplacePosts.Where(p => p.Status == MarketplacePostStatus.Active && p.ListingType == kind
                && (p.UpdatedAt >= schedule.StartedAt || schedule.BackfillBefore != null && p.CreatedAt <= schedule.BackfillBefore)
                && !db.AiListingReviews.Any(j => j.Kind == name && j.SubjectId == p.Id && j.SourceUpdatedAt >= p.UpdatedAt))
                .OrderByDescending(p => p.UpdatedAt).Take(3).ToListAsync(ct);
            foreach (var market in markets) await EnqueueAsync(market.Id, name, market.Title, Snapshot(market), market.UpdatedAt, ct);
        }
    }
    private async Task EnqueueAsync(Guid id, string kind, string title, string json, DateTimeOffset updatedAt, CancellationToken ct)
    {
        var hash = Hash(json);
        var existing = await db.AiListingReviews.SingleOrDefaultAsync(j => j.Kind == kind && j.SubjectId == id && j.ContentHash == hash, ct);
        if (existing is not null) existing.TouchSource(updatedAt);
        else db.Add(new AiListingReview(id, kind, title, hash, json, updatedAt, clock.GetUtcNow()));
        await db.SaveChangesAsync(ct);
    }
    private async Task<(string Json, DateTimeOffset UpdatedAt, RentalPost? Rental, MarketplacePost? Market)?> SourceAsync(AiListingReview job, CancellationToken ct)
    {
        if (job.Kind == "rental")
        {
            var p = await db.RentalPosts.FromSqlInterpolated($"SELECT * FROM homeji.rental_posts WHERE id = {job.SubjectId} FOR UPDATE").SingleOrDefaultAsync(ct);
            return p is null ? null : (Snapshot(p), p.UpdatedAt, p, null);
        }
        var market = await db.MarketplacePosts.FromSqlInterpolated($"SELECT * FROM homeji.marketplace_posts WHERE id = {job.SubjectId} FOR UPDATE").SingleOrDefaultAsync(ct);
        return market is null ? null : (Snapshot(market), market.UpdatedAt, null, market);
    }
    private static string Snapshot(RentalPost p) => JsonSerializer.Serialize(new { kind = "rental", p.Title, p.Description, p.Price, p.Area, p.Type }, JsonOptions);
    private static string Snapshot(MarketplacePost p) => JsonSerializer.Serialize(new { kind = p.ListingType == MarketplaceListingType.Food ? "food" : "goods", p.Title, p.Description, p.Price, p.Category, p.Condition, p.Unit }, JsonOptions);
    private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("policy-v1:" + content)));
}
