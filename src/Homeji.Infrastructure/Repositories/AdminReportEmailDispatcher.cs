using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Admin;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Infrastructure.Repositories;

public sealed class AdminReportEmailDispatcher(ApplicationDbContext db, ITransactionalEmailSender sender, TimeProvider clock) : IAdminReportEmailDispatcher
{
    public async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        // A crashed send may have reached the provider. Never silently resend an uncertain attachment.
        await db.AdminReportExports.Where(row => row.EmailStatus == "sending" && row.LeaseUntil < now)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.EmailStatus, "uncertain")
                .SetProperty(row => row.LastErrorCode, "outcome_uncertain")
                .SetProperty(row => row.LeaseId, (Guid?)null).SetProperty(row => row.LeaseUntil, (DateTimeOffset?)null), cancellationToken);
        var lease = Guid.NewGuid();
        var claimed = new List<Guid>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = """
                WITH picked AS (
                    SELECT "Id" FROM homeji.admin_report_exports
                    WHERE "EmailStatus"='queued' AND "Attempts"<8 AND "NextAttemptAt"<=@now
                    ORDER BY "NextAttemptAt","Id" FOR UPDATE SKIP LOCKED LIMIT 2
                )
                UPDATE homeji.admin_report_exports AS report
                SET "EmailStatus"='sending',"LeaseId"=@lease,"LeaseUntil"=@until,"Attempts"=report."Attempts"+1
                FROM picked WHERE report."Id"=picked."Id" RETURNING report."Id"
                """;
            command.Parameters.Add(new NpgsqlParameter("now", now));
            command.Parameters.Add(new NpgsqlParameter("lease", lease));
            command.Parameters.Add(new NpgsqlParameter("until", now.AddMinutes(10)));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) claimed.Add(reader.GetGuid(0));
        }
        foreach (var id in claimed)
        {
            var job = await db.AdminReportExports.AsNoTracking().SingleAsync(row => row.Id == id, cancellationToken);
            var text = $"Báo cáo quản trị Homeji, khoảng hoạt động {job.PeriodDays} ngày. File Excel đính kèm: {job.FileName}. Tài khoản và gói thành viên gồm dữ liệu hiện có tại lúc xuất.";
            var result = await sender.SendAsync(job.Recipient, $"[Homeji] Báo cáo quản trị {job.PeriodDays} ngày",
                $"<h2>Báo cáo quản trị Homeji</h2><p>{text}</p><p>Gói thành viên và thanh toán được trình bày riêng trong file.</p>", text,
                job.Id, cancellationToken, [new EmailAttachmentDto(job.FileName, Convert.ToBase64String(job.Content))]);
            var completed = clock.GetUtcNow();
            var status = result.Sent ? "accepted" : result.OutcomeUncertain ? "uncertain" : job.Attempts >= 8 ? "failed" : "queued";
            await db.AdminReportExports.Where(row => row.Id == id && row.LeaseId == lease)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.EmailStatus, status)
                    .SetProperty(row => row.EmailAcceptedAt, result.Sent ? completed : (DateTimeOffset?)null)
                    .SetProperty(row => row.LastErrorCode, result.Sent ? null : result.OutcomeUncertain ? "outcome_uncertain" : "send_failed")
                    .SetProperty(row => row.NextAttemptAt, completed.AddMinutes(Math.Min(360, Math.Pow(2, job.Attempts))))
                    .SetProperty(row => row.LeaseId, (Guid?)null).SetProperty(row => row.LeaseUntil, (DateTimeOffset?)null), cancellationToken);
        }
    }
}
