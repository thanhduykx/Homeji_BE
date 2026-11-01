using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.IRepositories.Admin;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Infrastructure.Repositories;

public sealed class AdminReportingRepository(ApplicationDbContext db, TimeProvider clock) : IAdminReportingRepository
{
    private static string Pattern(string keyword) => "%" + keyword.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
    internal IQueryable<AdminRegisteredAccountDto> AccountsQuery()
    {
        var now = clock.GetUtcNow();
        return db.Database.SqlQueryRaw<AdminRegisteredAccountDto>("""
            SELECT u.id AS "UserId", COALESCE(p.display_name,'Chưa cập nhật hồ sơ') AS "DisplayName",
                u.email AS "Email", p.role AS "Role", u.created_at AS "RegisteredAt",
                (u.email_confirmed_at IS NOT NULL) AS "EmailConfirmed", COALESCE(p.onboarding_completed,false) AS "ProfileCompleted",
                EXISTS (SELECT 1 FROM homeji.user_subscriptions s WHERE s.user_id=u.id AND s.tier=2 AND s.status=1
                    AND s.started_at<=@now AND s.expires_at>@now) AS "IsPremium",
                (SELECT max(s.expires_at) FROM homeji.user_subscriptions s WHERE s.user_id=u.id AND s.tier=2 AND s.status=1
                    AND s.started_at<=@now AND s.expires_at>@now) AS "PremiumExpiresAt"
            FROM auth.users u LEFT JOIN homeji.user_profiles p ON p.id=u.id WHERE u.deleted_at IS NULL
            """, new NpgsqlParameter("now", now));
    }
    internal IQueryable<AdminMemberDto> MembersQuery()
    {
        var now = clock.GetUtcNow();
        return db.Database.SqlQueryRaw<AdminMemberDto>("""
            SELECT s.id AS "Id",s.user_id AS "UserId",
                CASE WHEN u.id IS NULL THEN 'Tài khoản không còn hoạt động' ELSE COALESCE(p.display_name,'Chưa cập nhật hồ sơ') END AS "DisplayName",
                u.email AS "Email",s.package_code AS "PackageCode",s.package_name AS "PackageName",
                s.started_at AS "StartedAt",s.expires_at AS "ExpiresAt",s.created_at AS "RegisteredAt",
                CASE WHEN s.status=3 THEN 'cancelled' WHEN s.status=2 OR s.expires_at<=@now THEN 'expired'
                    WHEN s.started_at>@now THEN 'scheduled' ELSE 'active' END AS "State",
                s.payment_transaction_id AS "PaymentId",payment.amount AS "Amount",payment.status AS "PaymentStatus",payment.method AS "PaymentMethod"
            FROM homeji.user_subscriptions s
            LEFT JOIN auth.users u ON u.id=s.user_id AND u.deleted_at IS NULL
            LEFT JOIN homeji.user_profiles p ON p.id=u.id
            LEFT JOIN homeji.payment_transactions payment ON payment.id=s.payment_transaction_id
            WHERE s.tier=2
            """, new NpgsqlParameter("now", now));
    }
    public async Task<AdminPageDto<AdminRegisteredAccountDto>> GetAccountsAsync(string? keyword, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = AccountsQuery();
        if (!string.IsNullOrWhiteSpace(keyword)) { var pattern = Pattern(keyword); query = query.Where(row => EF.Functions.ILike(row.DisplayName, pattern, "\\") || (row.Email != null && EF.Functions.ILike(row.Email, pattern, "\\"))); }
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.RegisteredAt).ThenBy(row => row.UserId).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(rows, count, page, pageSize);
    }
    public async Task<AdminPageDto<AdminMemberDto>> GetMembersAsync(string? keyword, string? state, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = MembersQuery();
        if (!string.IsNullOrWhiteSpace(keyword)) { var pattern = Pattern(keyword); query = query.Where(row => EF.Functions.ILike(row.DisplayName, pattern, "\\")
            || (row.Email != null && EF.Functions.ILike(row.Email, pattern, "\\")) || EF.Functions.ILike(row.PackageName, pattern, "\\")); }
        if (!string.IsNullOrEmpty(state) && state != "all") query = query.Where(row => row.State == state);
        var count = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(row => row.RegisteredAt).ThenBy(row => row.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        return new(rows, count, page, pageSize);
    }
    private static void ValidateExportCount(int count)
    {
        if (count > 20000) throw new RequestValidationException(new Dictionary<string, string[]> { ["report"] = ["Báo cáo vượt quá 20.000 dòng mỗi danh sách. Cần chia nhỏ trước khi xuất."] });
    }
    public async Task<IReadOnlyList<AdminRegisteredAccountDto>> GetAllAccountsAsync(CancellationToken cancellationToken)
    {
        var rows = await AccountsQuery().OrderByDescending(row => row.RegisteredAt).ThenBy(row => row.UserId).Take(20001).ToArrayAsync(cancellationToken);
        ValidateExportCount(rows.Length); return rows;
    }
    public async Task<IReadOnlyList<AdminMemberDto>> GetAllMembersAsync(CancellationToken cancellationToken)
    {
        var rows = await MembersQuery().OrderByDescending(row => row.RegisteredAt).ThenBy(row => row.Id).Take(20001).ToArrayAsync(cancellationToken);
        ValidateExportCount(rows.Length); return rows;
    }
    public Task<AdminReportExport?> FindRequestAsync(Guid userId, Guid requestId, CancellationToken cancellationToken) =>
        db.AdminReportExports.AsNoTracking().SingleOrDefaultAsync(row => row.RequestedById == userId && row.RequestId == requestId, cancellationToken);
    public async Task<AdminReportExport> AddAsync(AdminReportExport report, CancellationToken cancellationToken)
    {
        db.AdminReportExports.Add(report);
        try { await db.SaveChangesAsync(cancellationToken); return report; }
        catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23505", ConstraintName: "IX_admin_report_exports_RequestedById_RequestId" })
        {
            db.Entry(report).State = EntityState.Detached;
            var existing = await FindRequestAsync(report.RequestedById, report.RequestId, cancellationToken);
            if (existing is null) throw;
            if (existing.PeriodDays != report.PeriodDays) throw new RequestValidationException(new Dictionary<string, string[]> { ["requestId"] = ["Mã yêu cầu đã được dùng cho khoảng thời gian khác."] });
            return existing;
        }
    }
    public async Task<IReadOnlyList<AdminReportExportDto>> GetExportsAsync(CancellationToken cancellationToken) =>
        await db.AdminReportExports.AsNoTracking().OrderByDescending(row => row.CreatedAt).ThenBy(row => row.Id).Take(30)
            .Select(row => new AdminReportExportDto(row.Id, row.FileName, row.PeriodDays, row.CreatedAt, row.Recipient,
                row.EmailStatus, row.EmailAcceptedAt, row.Attempts, row.LastErrorCode)).ToArrayAsync(cancellationToken);
    public Task<AdminReportFileDto?> DownloadAsync(Guid id, CancellationToken cancellationToken) =>
        db.AdminReportExports.AsNoTracking().Where(row => row.Id == id).Select(row => new AdminReportFileDto(row.Content, row.FileName)).SingleOrDefaultAsync(cancellationToken);
}
