using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.Reports;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IServices.Admin;
using Homeji.Application.Mappers.RentalPosts;
using Homeji.Application.Mappers.Reports;
using Homeji.Application.Services.Common;
using Homeji.Domain.Entities;
using Homeji.Domain.Enums;

namespace Homeji.Application.Services.Admin;

public sealed class AdminModerationService : IAdminModerationService
{
    private readonly UserContext _userContext;
    private readonly IRentalPostRepository _posts;
    private readonly IReportRepository _reports;
    private readonly INotificationRepository _notifications;
    private readonly TimeProvider _timeProvider;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly ISavedPostRepository _savedPosts;
    private readonly IUserProfileRepository _profiles;

    public AdminModerationService(
        UserContext userContext,
        IRentalPostRepository posts,
        IReportRepository reports,
        INotificationRepository notifications,
        ISavedPostRepository savedPosts,
        IUserProfileRepository profiles,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher)
    {
        _userContext = userContext;
        _posts = posts;
        _reports = reports;
        _notifications = notifications;
        _savedPosts = savedPosts;
        _profiles = profiles;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
    }

    public async Task<IReadOnlyList<RentalPostSummaryDto>> GetPendingRentalPostsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var posts = await _posts.GetPendingAsync(cancellationToken);
        return posts.Select(post => RentalPostMapper.ToSummaryDto(
            post,
            includePrivateTransferReview: true)).ToArray();
    }

    public async Task<RentalPostDto> ApproveRentalPostAsync(
        Guid postId,
        ApproveRentalPostDto request,
        CancellationToken cancellationToken = default)
    {
        var admin = await EnsureAdminAsync(cancellationToken);
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        post.Approve(
            _timeProvider.GetUtcNow(),
            post.Type == RentalPostType.RoomTransfer ? admin.Id : null,
            request.OwnerConsentVerificationNote);
        var notifications = new List<Notification>();
        var notification = new Notification(
            post.OwnerId,
            NotificationType.PostApproved,
            "Bài đăng đã được duyệt",
            $"Bài đăng '{post.Title}' đã được hiển thị công khai.",
            post.Id,
            _timeProvider.GetUtcNow());
        notifications.Add(notification);
        await _notifications.AddAsync(notification, cancellationToken);

        var savedByUsers = await _savedPosts.GetByPostAsync(post.Id, cancellationToken);
        foreach (var saved in savedByUsers.Where(saved => saved.UserId != post.OwnerId))
        {
            var changed = new Notification(
                saved.UserId,
                NotificationType.SavedPostChanged,
                "Bài đăng đã lưu được cập nhật",
                $"Bài '{post.Title}' đã được cập nhật. Giá hiện tại: {post.Price:N0} đồng.",
                post.Id,
                _timeProvider.GetUtcNow());
            notifications.Add(changed);
            await _notifications.AddAsync(changed, cancellationToken);
        }

        var matchingRenters = await _profiles.GetMatchingRentersAsync(post.Address, post.Price, post.OwnerId, 100, cancellationToken);
        var savedUserIds = savedByUsers.Select(saved => saved.UserId).ToHashSet();
        foreach (var renter in matchingRenters.Where(renter => !savedUserIds.Contains(renter.Id)))
        {
            var matched = new Notification(
                renter.Id,
                NotificationType.NewMatchingRentalPost,
                "Có phòng mới phù hợp",
                $"Bài '{post.Title}' phù hợp khu vực hoặc ngân sách của bạn.",
                post.Id,
                _timeProvider.GetUtcNow());
            notifications.Add(matched);
            await _notifications.AddAsync(matched, cancellationToken);
        }

        await _posts.SaveChangesAsync(cancellationToken);
        foreach (var item in notifications)
        {
            await _realtimePublisher.PublishAsync(item, cancellationToken);
        }
        return RentalPostMapper.ToDto(post);
    }

    public async Task<RentalPostDto> RejectRentalPostAsync(
        Guid postId,
        RejectRentalPostDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Bài đăng chưa đạt yêu cầu kiểm duyệt." : request.Reason!;
        post.Reject(reason, _timeProvider.GetUtcNow());
        var notification = new Notification(
            post.OwnerId,
            NotificationType.PostRejected,
            "Bài đăng bị từ chối",
            reason,
            post.Id,
            _timeProvider.GetUtcNow());
        await _notifications.AddAsync(notification, cancellationToken);
        await _posts.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return RentalPostMapper.ToDto(post);
    }

    public async Task<IReadOnlyList<ReportDto>> GetReportsAsync(
        ReportStatus? status,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var reports = await _reports.GetByStatusAsync(status, cancellationToken);
        return reports.Select(ReportMapper.ToDto).ToArray();
    }

    public async Task<ReportDto> ResolveReportAsync(
        Guid reportId,
        ResolveReportDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var report = await _reports.GetByIdAsync(reportId, cancellationToken)
            ?? throw new NotFoundException(nameof(Report), reportId);
        report.Resolve(request.Note, _timeProvider.GetUtcNow());
        var notification = new Notification(
            report.ReporterId,
            NotificationType.ReportResolved,
            "Báo cáo đã được xử lý",
            "Cảm ơn bạn đã gửi báo cáo. Đội ngũ Homeji đã xử lý báo cáo này.",
            report.Id,
            _timeProvider.GetUtcNow());
        await _notifications.AddAsync(notification, cancellationToken);
        await _reports.SaveChangesAsync(cancellationToken);
        await _realtimePublisher.PublishAsync(notification, cancellationToken);
        return ReportMapper.ToDto(report);
    }

    public async Task<ReportDto> RejectReportAsync(
        Guid reportId,
        ResolveReportDto request,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var report = await _reports.GetByIdAsync(reportId, cancellationToken)
            ?? throw new NotFoundException(nameof(Report), reportId);
        report.Reject(request.Note, _timeProvider.GetUtcNow());
        await _reports.SaveChangesAsync(cancellationToken);
        return ReportMapper.ToDto(report);
    }

    private async Task<UserProfile> EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(profile);
        return profile;
    }
}
