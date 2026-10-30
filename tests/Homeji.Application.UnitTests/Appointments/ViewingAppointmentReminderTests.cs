using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Appointments;

public sealed class ViewingAppointmentReminderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 1, 0, 0, TimeSpan.Zero);
    private static ViewingAppointment Appointment() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(2), null, Now);

    [Theory]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(60)]
    [InlineData(1440)]
    public void Reminder_stores_utc_due_time_and_repeated_request_keeps_delivery_key(int minutes)
    {
        var item = Appointment();
        item.SetEmailReminder(minutes, Now);
        var key = item.ReminderDeliveryId;
        item.SetEmailReminder(minutes, Now.AddMinutes(1));
        Assert.Equal(item.ScheduledAt.AddMinutes(-minutes), item.ReminderDueAt);
        Assert.Equal(key, item.ReminderDeliveryId);
        Assert.NotNull(key);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(int.MaxValue)]
    public void Invalid_interval_is_rejected(int minutes) =>
        Assert.Throws<DomainException>(() => Appointment().SetEmailReminder(minutes, Now));

    [Fact]
    public void Reminder_due_now_or_in_the_past_is_rejected()
    {
        var item = new ViewingAppointment(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Now.AddMinutes(15), null, Now);
        Assert.Throws<DomainException>(() => item.SetEmailReminder(15, Now));
        Assert.Throws<DomainException>(() => item.SetEmailReminder(30, Now));
    }

    [Fact]
    public void Reschedule_preserves_interval_and_replaces_delivery_key()
    {
        var item = Appointment();
        item.SetEmailReminder(60, Now);
        var key = item.ReminderDeliveryId;
        item.Reschedule(Now.AddDays(3), Now.AddMinutes(1));
        Assert.Equal(60, item.ReminderMinutesBefore);
        Assert.Equal(Now.AddDays(3).AddHours(-1), item.ReminderDueAt);
        Assert.NotEqual(key, item.ReminderDeliveryId);
    }

    [Fact]
    public void Reschedule_too_close_clears_reminder_instead_of_sending_at_old_time()
    {
        var item = Appointment();
        item.SetEmailReminder(60, Now);
        item.Reschedule(Now.AddMinutes(30), Now);
        Assert.Null(item.ReminderDueAt);
        Assert.Null(item.ReminderDeliveryId);
    }

    [Theory]
    [InlineData("cancel")]
    [InlineData("reject")]
    [InlineData("complete")]
    [InlineData("disable")]
    public void Ended_or_disabled_reminders_are_removed(string action)
    {
        var item = Appointment();
        item.SetEmailReminder(30, Now);
        switch (action)
        {
            case "cancel": item.Cancel(Now); break;
            case "reject": item.Reject(Now); break;
            case "complete": item.Confirm(Now); item.Complete(Now.AddDays(2)); break;
            default: item.SetEmailReminder(null, Now); break;
        }
        Assert.Null(item.ReminderDeliveryId);
        Assert.Null(item.ReminderDueAt);
        if (action != "disable") Assert.Throws<DomainException>(() => item.SetEmailReminder(15, Now));
    }
}
