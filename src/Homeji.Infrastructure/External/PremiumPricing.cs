using Homeji.Application.Common.Exceptions;
using Homeji.Application.IServices.Subscriptions;
using Homeji.Application.Services.Common;
using Homeji.Application.Services.Subscriptions;
using Homeji.Domain.Entities;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
namespace Homeji.Infrastructure.External;

public sealed class PremiumPricing(ApplicationDbContext db, UserContext users, IOptions<PremiumSubscriptionOptions> options, TimeProvider clock) : IPremiumPricing
{
    public async Task<IReadOnlyList<PremiumPlanOptions>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        var changes = await db.PremiumPriceChanges.AsNoTracking()
            .GroupBy(row => row.PackageCode).Select(group => group.OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id).First()).ToListAsync(cancellationToken);
        return options.Value.GetPlansAt(clock.GetUtcNow()).Select(plan =>
        {
            var change = changes.Find(row => row.PackageCode == plan.Code);
            return change is null ? plan : new PremiumPlanOptions { Code = plan.Code, Name = plan.Name, DurationDays = plan.DurationDays, Price = change.Price };
        }).ToArray();
    }
    public async Task<IReadOnlyList<PremiumPriceAnnouncement>> GetAnnouncementsAsync(CancellationToken cancellationToken = default) =>
        await db.PremiumPriceChanges.AsNoTracking().OrderByDescending(row => row.CreatedAt).ThenByDescending(row => row.Id).Take(30)
            .Select(row => new PremiumPriceAnnouncement(row.Id, row.PackageCode, row.PackageName, row.PreviousPrice, row.Price, row.Reason, row.CreatedAt)).ToArrayAsync(cancellationToken);

    public async Task<PremiumPriceAnnouncement> ChangeAsync(string code, ChangePremiumPriceRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await users.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(admin);
        var reason = request.Reason?.Trim() ?? string.Empty;
        if (request.RequestId == Guid.Empty || request.Price is < 1000 or > 100_000_000 || decimal.Truncate(request.Price) != request.Price || reason.Length is < 5 or > 500)
            throw Invalid("Nhập giá nguyên từ 1.000 đến 100.000.000 đ và lý do từ 5 đến 500 ký tự.");
        code = code.Trim().ToUpperInvariant();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            foreach (var entry in db.ChangeTracker.Entries<PremiumPriceChange>().ToArray()) entry.State = EntityState.Detached;
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            // One shared lock orders all price changes, including concurrent admins/retries.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(7331192001)", cancellationToken);
            var previous = await db.PremiumPriceChanges.AsNoTracking().SingleOrDefaultAsync(row => row.RequestId == request.RequestId, cancellationToken);
            if (previous is not null)
            {
                if (previous.AdminId != admin.Id || previous.PackageCode != code || previous.Price != request.Price || previous.PreviousPrice != request.ExpectedPrice || previous.Reason != reason)
                    throw Invalid("Mã yêu cầu đã được dùng cho thay đổi khác.");
                await transaction.CommitAsync(cancellationToken);
                return ToAnnouncement(previous);
            }
            var plan = (await GetPlansAsync(cancellationToken)).SingleOrDefault(row => row.Code == code)
                ?? throw Invalid("Không tìm thấy gói thành viên.");
            if (plan.Price != request.ExpectedPrice) throw Invalid("Giá vừa được cập nhật. Tải lại bảng giá trước khi sửa.");
            if (plan.Price == request.Price) throw Invalid("Giá mới phải khác giá hiện tại.");
            var change = new PremiumPriceChange { AdminId = admin.Id, RequestId = request.RequestId, PackageCode = code, PackageName = plan.Name,
                PreviousPrice = plan.Price, Price = request.Price, Reason = reason, CreatedAt = clock.GetUtcNow() };
            db.PremiumPriceChanges.Add(change);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return ToAnnouncement(change);
        });
    }
    private static PremiumPriceAnnouncement ToAnnouncement(PremiumPriceChange row) => new(row.Id, row.PackageCode, row.PackageName, row.PreviousPrice, row.Price, row.Reason, row.CreatedAt);
    private static RequestValidationException Invalid(string message) => new(new Dictionary<string, string[]> { ["price"] = [message] });
}
