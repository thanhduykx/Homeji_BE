using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Homeji.Infrastructure.Repositories;

public sealed class ReceiptEmailDispatcher(ApplicationDbContext db, ITransactionalEmailSender sender,
    IOptions<ReceiptEmailOptions> options, TimeProvider timeProvider) : IReceiptEmailDispatcher
{
    public async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return;
        var now = timeProvider.GetUtcNow();
        // A crash can leave an accepted send unacknowledged. After the provider deduplication
        // window, stop automatic retries and require reconciliation instead of risking duplicates.
        await db.ReceiptEmailDeliveries.Where(x => x.SentAt == null && x.Attempts < 8
                && x.LeaseUntil != null && x.LeaseUntil < now.AddMinutes(-5))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, 8)
                .SetProperty(x => x.LastErrorCode, "outcome_uncertain")
                .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(x => x.LeaseId, (Guid?)null), cancellationToken);
        var lease = Guid.NewGuid();
        var claimed = new List<Guid>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = """
                WITH picked AS (
                    SELECT "Id" FROM homeji.receipt_email_deliveries
                    WHERE "SentAt" IS NULL AND "Attempts" < 8 AND "NextAttemptAt" <= @now
                      AND ("LeaseUntil" IS NULL OR "LeaseUntil" <= @now)
                    ORDER BY "NextAttemptAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 5
                )
                UPDATE homeji.receipt_email_deliveries AS d
                SET "LeaseId" = @lease, "LeaseUntil" = @until, "Attempts" = d."Attempts" + 1
                FROM picked WHERE d."Id" = picked."Id" RETURNING d."Id"
                """;
            command.Parameters.Add(new NpgsqlParameter("now", now));
            command.Parameters.Add(new NpgsqlParameter("lease", lease));
            command.Parameters.Add(new NpgsqlParameter("until", now.AddMinutes(20)));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) claimed.Add(reader.GetGuid(0));
        }

        if (claimed.Count == 0) return;
        var jobs = await db.ReceiptEmailDeliveries.AsNoTracking().Where(x => claimed.Contains(x.Id)).ToArrayAsync(cancellationToken);
        var receiptIds = jobs.Select(x => x.ReceiptId).ToArray();
        var receipts = await db.TransactionReceipts.AsNoTracking().Where(x => receiptIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var userIds = receipts.Values.Select(x => x.UserId).Distinct().ToArray();
        var recipients = await db.Database.SqlQueryRaw<ReceiptRecipient>(
            "SELECT id AS \"Id\", email AS \"Email\" FROM auth.users WHERE id = ANY(@ids) AND email_confirmed_at IS NOT NULL AND email IS NOT NULL",
            new NpgsqlParameter("ids", userIds)).ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);

        foreach (var job in jobs)
        {
            var receipt = receipts[job.ReceiptId];
            var dto = new TransactionReceiptDto(receipt.Id, receipt.ReferenceId, receipt.Event, receipt.Amount, "VND",
                receipt.ReferenceCode, receipt.Description, receipt.IssuedAt, null, job.Attempts, false);
            var result = new EmailSendResultDto(false, "recipient_unavailable");
            var failureCode = "recipient_unavailable";
            if (recipients.TryGetValue(receipt.UserId, out var email))
            {
                var html = TransactionReceiptHtml.Render(dto);
                IReadOnlyList<EmailAttachmentDto>? attachments = null;
                try { attachments = TransactionReceiptFiles.Create(dto, html); }
                catch (Exception error) when (error is InvalidOperationException or IOException or DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
                {
                    // No provider call occurred: this is a known document failure, safe to retry.
                    failureCode = "receipt_files_failed";
                }
                if (attachments is not null)
                {
                    failureCode = "send_failed";
                    result = await sender.SendAsync(email, "[Homeji] " + TransactionReceiptHtml.Title(receipt.Event),
                        html, $"Biên nhận Homeji {ReceiptPresentation.Id(dto)}. {TransactionReceiptHtml.Title(receipt.Event)}. Số tiền: {ReceiptPresentation.Amount(dto)}. Mã giao dịch: {receipt.ReferenceCode}. {ReceiptPresentation.Note(receipt.Event)}",
                        job.Id, cancellationToken, attachments);
                }
            }

            var completedAt = timeProvider.GetUtcNow();
            var nextAttempt = completedAt.AddMinutes(Math.Min(360, Math.Pow(2, job.Attempts)));
            // Guard against a stale worker acknowledging another worker's renewed lease.
            await db.ReceiptEmailDeliveries.Where(x => x.Id == job.Id && x.LeaseId == lease)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(x => x.SentAt, result.Sent ? completedAt : (DateTimeOffset?)null)
                    .SetProperty(x => x.LastErrorCode, result.Sent ? null : result.OutcomeUncertain ? "outcome_uncertain" : failureCode)
                    .SetProperty(x => x.Attempts, result.OutcomeUncertain ? 8 : job.Attempts)
                    .SetProperty(x => x.NextAttemptAt, nextAttempt)
                    .SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null)
                    .SetProperty(x => x.LeaseId, (Guid?)null), cancellationToken);
        }
    }

    private sealed record ReceiptRecipient(Guid Id, string Email);
}
