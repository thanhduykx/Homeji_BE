using Homeji.Domain.Entities;
using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Application.UnitTests.Appointments;

public sealed class ViewingAppointmentTests
{
    [Fact]
    public void Constructor_WithPastSchedule_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() => new ViewingAppointment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddMinutes(-1), null, now));
    }

    [Fact]
    public void Confirm_WhenPending_ChangesStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = new ViewingAppointment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddDays(1), null, now);

        appointment.Confirm(now.AddMinutes(1));

        Assert.Equal(ViewingAppointmentStatus.Confirmed, appointment.Status);
    }

    [Fact]
    public void Reject_WhenAlreadyConfirmed_ThrowsDomainException()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = new ViewingAppointment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddDays(1), null, now);
        appointment.Confirm(now.AddMinutes(1));

        Assert.Throws<DomainException>(() => appointment.Reject(now.AddMinutes(2)));
    }

    [Fact]
    public void Reschedule_WhenConfirmed_MovesBackToPending()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = new ViewingAppointment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddDays(1), null, now);
        appointment.Confirm(now.AddMinutes(1));

        appointment.Reschedule(now.AddDays(2), now.AddMinutes(2));

        Assert.Equal(ViewingAppointmentStatus.Pending, appointment.Status);
        Assert.Equal(now.AddDays(2), appointment.ScheduledAt);
    }

    [Fact]
    public void Complete_WhenConfirmed_ChangesStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var appointment = new ViewingAppointment(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), now.AddDays(1), null, now);
        appointment.Confirm(now.AddMinutes(1));

        appointment.Complete(now.AddDays(1).AddMinutes(1));

        Assert.Equal(ViewingAppointmentStatus.Completed, appointment.Status);
    }
}
