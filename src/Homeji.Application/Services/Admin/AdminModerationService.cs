using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.Reports;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
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

    public AdminModerationService(
        UserContext userContext,
        IRentalPostRepository posts,
        IReportRepository reports,
        INotificationRepository notifications,
        TimeProvider timeProvider)
    {
        _userContext = userContext;
        _posts = posts;
        _reports = reports;
        _notifications = notifications;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<RentalPostSummaryDto>> GetPendingRentalPostsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var posts = await _posts.GetPendingAsync(cancellationToken);
        return posts.Select(post => RentalPostMapper.ToSummaryDto(post)).ToArray();
    }

    public async Task<RentalPostDto> ApproveRentalPostAsync(Guid postId, CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var post = await _posts.GetByIdWithMediaAsync(postId, cancellationToken)
            ?? throw new NotFoundException(nameof(RentalPost), postId);
        post.Approve(_timeProvider.GetUtcNow());
        await _notifications.AddAsync(new Notification(
            post.OwnerId,
            NotificationType.PostApproved,
            "Bài đăng đã được duyệt",
            $"Bài đăng '{post.Title}' đã được hiển thị công khai.",
            post.Id,
            _timeProvider.GetUtcNow()), cancellationToken);
        await _posts.SaveChangesAsync(cancellationToken);
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
        await _notifications.AddAsync(new Notification(
            post.OwnerId,
            NotificationType.PostRejected,
            "Bài đăng bị từ chối",
            reason,
            post.Id,
            _timeProvider.GetUtcNow()), cancellationToken);
        await _posts.SaveChangesAsync(cancellationToken);
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
        await _notifications.AddAsync(new Notification(
            report.ReporterId,
            NotificationType.ReportResolved,
            "Báo cáo đã được xử lý",
            "Cảm ơn bạn đã gửi báo cáo. Đội ngũ Homeji đã xử lý báo cáo này.",
            report.Id,
            _timeProvider.GetUtcNow()), cancellationToken);
        await _reports.SaveChangesAsync(cancellationToken);
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

    private async Task EnsureAdminAsync(CancellationToken cancellationToken)
    {
        var profile = await _userContext.GetRequiredProfileAsync(cancellationToken);
        UserContext.EnsureAdmin(profile);
    }
}
