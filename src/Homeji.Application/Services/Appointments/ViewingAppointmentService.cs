using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Appointments;
using Homeji.Application.IRepositories.Appointments;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IServices.Appointments;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Appointments;

public sealed class ViewingAppointmentService : IViewingAppointmentService
{
    private readonly UserContext _userContext;
    private readonly IViewingAppointmentRepository _appointments;
    private readonly IRentalPostRepository _posts;
    private readonly INotificationRepository _notifications;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly TimeProvider _timeProvider;

    public ViewingAppointmentService(
        UserContext userContext,
        IViewingAppointmentRepository appointments,
        IRentalPostRepository posts,
        INotificationRepository notifications,
        INotificationRealtimePublisher realtimePublisher,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _appointments = appointments;
        _posts = posts;
        _notifications = notifications;
        _realtimePublisher = realtimePublisher;
        _timeProvider = timeProvider;
    }

    public async Task<ViewingAppointmentDto> CreateAsync(
        Guid rentalPostId,
        CreateViewingAppointmentDto request,
        CancellationToken cancellationToken = default)
    {
        var renter = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureRenter(renter);
        var requesterId = renter.Id;
        var post = await _posts.GetByIdAsync(rentalPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), rentalPostId);
        if (post.Status != RentalPostStatus.Active)
        {
            throw new NotFoundException(nameof(RentalPost), rentalPostId);
        }

        if (post.OwnerId == requesterId)
        {
            throw new ForbiddenAccessException("Rental post owners cannot request a viewing for their own post.");
        }

        if (await _appointments.HasActiveAsync(rentalPostId, requesterId, cancellationToken))
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["rentalPostId"] = ["You already have an active viewing appointment for this rental post."],
            });
        }

        var now = _timeProvider.GetUtcNow();
        var appointment = new ViewingAppointment(
            rentalPostId,
            requesterId,
            post.OwnerId,
            request.ScheduledAt,
            request.Note,
            now);
        var notification = new Notification(
            post.OwnerId,
            NotificationType.ViewingAppointmentRequested,
            "Yêu cầu xem phòng mới",
            $"Có người muốn đặt lịch xem bài đăng '{post.Title}'.",
            appointment.Id,
            now);

        await _appointments.AddAsync(appointment, cancellationToken);
        await _notifications.AddAsync(notification, cancellationToken);
        await _appointments.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(appointment, post.Title);
    }

    public async Task<IReadOnlyList<ViewingAppointmentDto>> GetMineAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var appointments = await _appointments.GetForUserAsync(userId, cancellationToken);
        var posts = await _posts.GetByIdsAsync(
            appointments.Select(item => item.RentalPostId).Distinct().ToArray(),
            cancellationToken);
        var titles = posts.ToDictionary(post => post.Id, post => post.Title);
        return appointments
            .Select(item => ToDto(item, titles.GetValueOrDefault(item.RentalPostId) ?? "Rental post"))
            .ToArray();
    }

    public Task<ViewingAppointmentDto> ConfirmAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, ViewingAppointmentStatus.Confirmed, cancellationToken);
    }

    public Task<ViewingAppointmentDto> RejectAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, ViewingAppointmentStatus.Rejected, cancellationToken);
    }

    public Task<ViewingAppointmentDto> CancelAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return UpdateAsync(id, ViewingAppointmentStatus.Cancelled, cancellationToken);
    }

    public async Task<ViewingAppointmentDto> RescheduleAsync(
        Guid id,
        RescheduleViewingAppointmentDto request,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var appointment = await _appointments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ViewingAppointment), id);
        if (userId != appointment.RequesterId && userId != appointment.OwnerId)
        {
            throw new ForbiddenAccessException("Only appointment participants can propose another viewing time.");
        }

        var post = await _posts.GetByIdAsync(appointment.RentalPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), appointment.RentalPostId);
        var now = _timeProvider.GetUtcNow();
        appointment.Reschedule(request.ScheduledAt, now);
        var recipientId = userId == appointment.RequesterId ? appointment.OwnerId : appointment.RequesterId;
        var notification = BuildUpdateNotification(appointment, post.Title, recipientId, now);
        await _notifications.AddAsync(notification, cancellationToken);
        await _appointments.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(appointment, post.Title);
    }

    public async Task<ViewingAppointmentDto> CompleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.GetRequiredUserId();
        var appointment = await _appointments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ViewingAppointment), id);
        UserContext.EnsureOwner(userId, appointment.OwnerId);
        var post = await _posts.GetByIdAsync(appointment.RentalPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), appointment.RentalPostId);
        var now = _timeProvider.GetUtcNow();
        appointment.Complete(now);
        var notification = BuildUpdateNotification(appointment, post.Title, appointment.RequesterId, now);
        await _notifications.AddAsync(notification, cancellationToken);
        await _appointments.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(appointment, post.Title);
    }

    private async Task<ViewingAppointmentDto> UpdateAsync(
        Guid id,
        ViewingAppointmentStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var userId = _userContext.GetRequiredUserId();
        var appointment = await _appointments.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ViewingAppointment), id);
        var post = await _posts.GetByIdAsync(appointment.RentalPostId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), appointment.RentalPostId);

        if (targetStatus == ViewingAppointmentStatus.Cancelled)
        {
            UserContext.EnsureOwner(userId, appointment.RequesterId);
            appointment.Cancel(_timeProvider.GetUtcNow());
        }
        else
        {
            UserContext.EnsureOwner(userId, appointment.OwnerId);
            if (targetStatus == ViewingAppointmentStatus.Confirmed)
            {
                appointment.Confirm(_timeProvider.GetUtcNow());
            }
            else
            {
                appointment.Reject(_timeProvider.GetUtcNow());
            }
        }

        var notification = BuildUpdateNotification(
            appointment,
            post.Title,
            targetStatus == ViewingAppointmentStatus.Cancelled ? appointment.OwnerId : appointment.RequesterId,
            _timeProvider.GetUtcNow());
        await _notifications.AddAsync(notification, cancellationToken);
        await _appointments.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ToDto(appointment, post.Title);
    }

    private static Notification BuildUpdateNotification(
        ViewingAppointment appointment,
        string postTitle,
        Guid recipientId,
        DateTimeOffset now)
    {
        return new Notification(
            recipientId,
            NotificationType.ViewingAppointmentUpdated,
            "Lịch xem phòng đã cập nhật",
            $"Lịch xem bài đăng '{postTitle}' hiện ở trạng thái {appointment.Status}.",
            appointment.Id,
            now);
    }

    private static ViewingAppointmentDto ToDto(ViewingAppointment appointment, string postTitle)
    {
        return new ViewingAppointmentDto(
            appointment.Id,
            appointment.RentalPostId,
            postTitle,
            appointment.RequesterId,
            appointment.OwnerId,
            appointment.ScheduledAt,
            appointment.Note,
            appointment.Status,
            appointment.CreatedAt,
            appointment.UpdatedAt);
    }
}
