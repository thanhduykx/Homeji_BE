using Homeji.Domain.Enums;
using Homeji.Domain.Exceptions;

namespace Homeji.Domain.Entities;

public sealed class ViewingAppointment
{
    public const int MaxNoteLength = 500;

    private ViewingAppointment()
    {
    }

    public ViewingAppointment(
        Guid rentalPostId,
        Guid requesterId,
        Guid ownerId,
        DateTimeOffset scheduledAt,
        string? note,
        DateTimeOffset createdAt)
    {
        if (scheduledAt <= createdAt)
        {
            throw new DomainException("Thời gian xem phòng phải ở tương lai.");
        }

        Id = Guid.NewGuid();
        RentalPostId = rentalPostId;
        RequesterId = requesterId;
        OwnerId = ownerId;
        ScheduledAt = scheduledAt;
        Note = NormalizeOptional(note, MaxNoteLength, nameof(Note));
        Status = ViewingAppointmentStatus.Pending;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid RentalPostId { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid OwnerId { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public string? Note { get; private set; }
    public ViewingAppointmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public void Confirm(DateTimeOffset updatedAt)
    {
        EnsurePending();
        Status = ViewingAppointmentStatus.Confirmed;
        UpdatedAt = updatedAt;
    }

    public void Reject(DateTimeOffset updatedAt)
    {
        EnsurePending();
        Status = ViewingAppointmentStatus.Rejected;
        UpdatedAt = updatedAt;
    }

    public void Cancel(DateTimeOffset updatedAt)
    {
        if (Status is ViewingAppointmentStatus.Rejected or ViewingAppointmentStatus.Cancelled or ViewingAppointmentStatus.Completed)
        {
            throw new DomainException("Lịch xem phòng này không còn hủy được nữa.");
        }

        Status = ViewingAppointmentStatus.Cancelled;
        UpdatedAt = updatedAt;
    }

    public void Reschedule(DateTimeOffset scheduledAt, DateTimeOffset updatedAt)
    {
        if (Status is ViewingAppointmentStatus.Rejected or ViewingAppointmentStatus.Cancelled or ViewingAppointmentStatus.Completed)
        {
            throw new DomainException("Lịch xem phòng này không còn đổi giờ được nữa.");
        }

        if (scheduledAt <= updatedAt)
        {
            throw new DomainException("Thời gian xem phòng phải ở tương lai.");
        }

        ScheduledAt = scheduledAt;
        Status = ViewingAppointmentStatus.Pending;
        UpdatedAt = updatedAt;
    }

    public void Complete(DateTimeOffset updatedAt)
    {
        if (Status != ViewingAppointmentStatus.Confirmed)
        {
            throw new DomainException("Chỉ lịch xem phòng đã xác nhận mới có thể hoàn tất.");
        }

        Status = ViewingAppointmentStatus.Completed;
        UpdatedAt = updatedAt;
    }

    private void EnsurePending()
    {
        if (Status != ViewingAppointmentStatus.Pending)
        {
            throw new DomainException("Chỉ lịch xem phòng đang chờ mới được chủ tin cập nhật.");
        }
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException($"{fieldName} không được vượt quá {maxLength} ký tự.");
        }

        return normalized;
    }
}
