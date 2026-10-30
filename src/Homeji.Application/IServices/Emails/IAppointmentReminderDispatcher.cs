namespace Homeji.Application.IServices.Emails;

public interface IAppointmentReminderDispatcher
{
    Task DispatchBatchAsync(CancellationToken cancellationToken);
}
