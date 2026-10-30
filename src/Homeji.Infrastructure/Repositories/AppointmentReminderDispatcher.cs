using Homeji.Application.Common;
using Homeji.Application.DTOs.Emails;
using Homeji.Application.IServices.Emails;
using Homeji.Infrastructure.Context;
using Homeji.Infrastructure.External;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Homeji.Infrastructure.Repositories;

public sealed class AppointmentReminderDispatcher(ApplicationDbContext db, ITransactionalEmailSender sender,
    TimeProvider timeProvider) : IAppointmentReminderDispatcher
{
    public async Task DispatchBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        // An expired claim may have sent successfully before a crash. Do not resend uncertain email.
        await db.ViewingAppointments.Where(x => x.ReminderSentAt == null && x.ReminderLeaseUntil < now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReminderAttempts, 5)
                .SetProperty(x => x.ReminderLeaseUntil, (DateTimeOffset?)null), cancellationToken);
        var claimed = new List<(Guid Id, Guid DeliveryId)>();
        var until = now.AddMinutes(20);
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using (var command = db.Database.GetDbConnection().CreateCommand())
        {
            command.CommandText = """
                WITH picked AS (
                    SELECT "Id" FROM homeji.viewing_appointments
                    WHERE "ReminderDeliveryId" IS NOT NULL AND "ReminderSentAt" IS NULL
                      AND "ReminderAttempts" < 5 AND "ReminderNextAttemptAt" <= @now
                      AND "ReminderLeaseUntil" IS NULL AND "ScheduledAt" > @now AND "Status" IN (0, 1)
                    ORDER BY "ReminderNextAttemptAt", "Id" FOR UPDATE SKIP LOCKED LIMIT 5
                )
                UPDATE homeji.viewing_appointments AS a
                SET "ReminderLeaseUntil" = @until, "ReminderAttempts" = a."ReminderAttempts" + 1
                FROM picked WHERE a."Id" = picked."Id" RETURNING a."Id", a."ReminderDeliveryId"
                """;
            command.Parameters.Add(new NpgsqlParameter("now", now));
            command.Parameters.Add(new NpgsqlParameter("until", until));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken)) claimed.Add((reader.GetGuid(0), reader.GetGuid(1)));
        }
        foreach (var (id, deliveryId) in claimed)
        {
            // Re-read after claiming so cancelled or rescheduled jobs are never sent from the old batch snapshot.
            var sendAt = timeProvider.GetUtcNow();
            var appointment = await db.ViewingAppointments.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id && x.ReminderLeaseUntil == until
                    && x.ReminderDeliveryId == deliveryId && x.ReminderSentAt == null
                    && x.ReminderNextAttemptAt <= sendAt && x.ScheduledAt > sendAt
                    && (x.Status == Domain.Enums.ViewingAppointmentStatus.Pending
                        || x.Status == Domain.Enums.ViewingAppointmentStatus.Confirmed), cancellationToken);
            if (appointment is null) continue;
            var post = await db.RentalPosts.AsNoTracking().SingleAsync(x => x.Id == appointment.RentalPostId, cancellationToken);
            var email = await db.Database.SqlQueryRaw<string>("""
                SELECT email AS "Value" FROM auth.users
                WHERE id = @id AND email_confirmed_at IS NOT NULL AND email IS NOT NULL
                """, new NpgsqlParameter("id", appointment.RequesterId)).SingleOrDefaultAsync(cancellationToken);
            var result = new EmailSendResultDto(false, "recipient_or_post_unavailable");
            if (email is not null && RentalPublicAvailability.IsPublicActive(post, RentalPublicAvailability.Today(timeProvider)))
            {
                var content = AppointmentReminderEmail.Render(appointment, post);
                result = await sender.SendAsync(email, AppointmentReminderEmail.Subject, content.Html, content.Text,
                    appointment.ReminderDeliveryId!.Value, cancellationToken);
            }
            var completedAt = timeProvider.GetUtcNow();
            await db.ViewingAppointments.Where(x => x.Id == id
                    && x.ReminderDeliveryId == appointment.ReminderDeliveryId && x.ReminderLeaseUntil == until)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.ReminderSentAt, result.Sent ? completedAt : (DateTimeOffset?)null)
                    .SetProperty(x => x.ReminderAttempts, result.OutcomeUncertain ? 5 : appointment.ReminderAttempts)
                    .SetProperty(x => x.ReminderNextAttemptAt, completedAt.AddMinutes(1))
                    .SetProperty(x => x.ReminderLeaseUntil, (DateTimeOffset?)null), cancellationToken);
        }
    }
}
