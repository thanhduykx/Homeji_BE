using Homeji.Application.Abstractions.Notifications;
using Homeji.Application.Abstractions.Presence;
using Homeji.Application.Abstractions.Authentication;
using Homeji.Application.Common.Exceptions;
using Homeji.Application.DTOs.Admin;
using Homeji.Application.DTOs.RentalPosts;
using Homeji.Application.DTOs.Reports;
using Homeji.Application.IRepositories.Notifications;
using Homeji.Application.IRepositories.Marketplace;
using Homeji.Application.IRepositories.RentalPosts;
using Homeji.Application.IRepositories.Reports;
using Homeji.Application.IRepositories.Reviews;
using Homeji.Application.IRepositories.Roommates;
using Homeji.Application.IRepositories.SavedPosts;
using Homeji.Application.IRepositories.Profiles;
using Homeji.Application.IRepositories.WantedPosts;
using Homeji.Application.IServices.Admin;
using Homeji.Application.IServices.Accounts;
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
    private readonly IOnlineUserTracker _onlineUsers;
    private readonly IMarketplacePostRepository _marketplacePosts;
    private readonly IRoommateInvitationRepository _roommateInvitations;
    private readonly IRentalReviewRepository _reviews;
    private readonly IRentalWantedPostRepository _wantedPosts;
    private readonly IUserSessionRevocationService _sessionRevocations;
    private readonly IUserSessionRealtimePublisher _sessionRealtimePublisher;

    public AdminModerationService(
        UserContext userContext,
        IRentalPostRepository posts,
        IReportRepository reports,
        INotificationRepository notifications,
        ISavedPostRepository savedPosts,
        IUserProfileRepository profiles,
        IOnlineUserTracker onlineUsers,
        IMarketplacePostRepository marketplacePosts,
        IRoommateInvitationRepository roommateInvitations,
        IRentalReviewRepository reviews,
        IRentalWantedPostRepository wantedPosts,
        IUserSessionRevocationService sessionRevocations,
        IUserSessionRealtimePublisher sessionRealtimePublisher,
        TimeProvider timeProvider,
        INotificationRealtimePublisher realtimePublisher)
    {
        _userContext = userContext;
        _posts = posts;
        _reports = reports;
        _notifications = notifications;
        _savedPosts = savedPosts;
        _profiles = profiles;
        _onlineUsers = onlineUsers;
        _marketplacePosts = marketplacePosts;
        _roommateInvitations = roommateInvitations;
        _reviews = reviews;
        _wantedPosts = wantedPosts;
        _sessionRevocations = sessionRevocations;
        _sessionRealtimePublisher = sessionRealtimePublisher;
        _timeProvider = timeProvider;
        _realtimePublisher = realtimePublisher;
    }

    public async Task<IReadOnlyList<AdminActiveUserDto>> GetActiveUsersAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var onlineByUser = _onlineUsers.GetOnlineUsers();
        var profiles = await _profiles.GetByIdsAsync(onlineByUser.Keys.ToArray(), cancellationToken);

        return profiles
            .Where(profile => onlineByUser.ContainsKey(profile.Id))
            .Select(profile =>
            {
                var lastSeenAt = onlineByUser[profile.Id];
                return new AdminActiveUserDto(
                    profile.Id,
                    profile.DisplayName,
                    profile.Role,
                    profile.AvatarPath,
                    lastSeenAt,
                    true);
            })
            .OrderByDescending(user => user.IsOnline)
            .ThenByDescending(user => user.LastSeenAt)
            .ToArray();
    }

    public async Task<TerminateUserSessionResultDto> TerminateUserSessionAsync(
        Guid userId,
        TerminateUserSessionRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var admin = await EnsureAdminAsync(cancellationToken);
        if (admin.Id == userId)
        {
            throw new ConflictException("Không thể kết thúc phiên đăng nhập đang dùng để quản trị.");
        }

        var target = await _profiles.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(UserProfile), userId);
        var reason = string.IsNullOrWhiteSpace(request.Reason)
            ? "Quản trị viên đã kết thúc phiên đăng nhập của bạn."
            : request.Reason.Trim();
        if (reason.Length > 300)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["reason"] = ["Lý do không được vượt quá 300 ký tự."],
            });
        }

        var terminatedAt = await _sessionRevocations.RevokeAsync(
            userId,
            _timeProvider.GetUtcNow(),
            cancellationToken);
        await _sessionRealtimePublisher.TerminateAsync(
            userId,
            reason,
            terminatedAt,
            cancellationToken);

        return new TerminateUserSessionResultDto(
            userId,
            target.DisplayName,
            reason,
            terminatedAt);
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

    public async Task<IReadOnlyList<AdminReportDto>> GetReportsAsync(
        ReportStatus? status,
        CancellationToken cancellationToken = default)
    {
        await EnsureAdminAsync(cancellationToken);
        var reports = await _reports.GetByStatusAsync(status, cancellationToken);
        if (reports.Count == 0) return [];

        var profileIds = reports
            .Select(report => report.ReporterId)
            .Concat(reports
                .Where(report => report.TargetType == ReportTargetType.User)
                .Select(report => report.TargetId))
            .Distinct()
            .ToArray();
        var profiles = (await _profiles.GetByIdsAsync(profileIds, cancellationToken))
            .ToDictionary(profile => profile.Id);

        var marketplaceIds = GetTargetIds(reports, ReportTargetType.MarketplacePost);
        var marketplacePosts = (await _marketplacePosts.GetByIdsAsync(marketplaceIds, cancellationToken))
            .ToDictionary(post => post.Id);

        var invitationIds = GetTargetIds(reports, ReportTargetType.RoommateInvitation);
        var invitations = (await _roommateInvitations.GetByIdsAsync(invitationIds, cancellationToken))
            .ToDictionary(invitation => invitation.Id);

        var reviewIds = GetTargetIds(reports, ReportTargetType.RentalReview);
        var reviews = (await _reviews.GetByIdsAsync(reviewIds, cancellationToken))
            .ToDictionary(review => review.Id);

        var wantedIds = GetTargetIds(reports, ReportTargetType.RentalWantedPost);
        var wantedPosts = (await _wantedPosts.GetByIdsAsync(wantedIds, cancellationToken))
            .ToDictionary(post => post.Id);

        var rentalPostIds = GetTargetIds(reports, ReportTargetType.RentalPost)
            .Concat(invitations.Values.Select(invitation => invitation.RentalPostId))
            .Concat(reviews.Values.Select(review => review.RentalPostId))
            .Distinct()
            .ToArray();
        var rentalPosts = (await _posts.GetByIdsWithMediaAsync(rentalPostIds, cancellationToken))
            .ToDictionary(post => post.Id);

        return reports.Select(report =>
        {
            var target = ResolveReportTarget(
                report,
                profiles,
                rentalPosts,
                marketplacePosts,
                invitations,
                reviews,
                wantedPosts);
            return new AdminReportDto(
                report.Id,
                report.ReporterId,
                profiles.TryGetValue(report.ReporterId, out var reporter)
                    ? reporter.DisplayName
                    : "Tài khoản không còn tồn tại",
                report.TargetType,
                report.TargetId,
                target.DisplayName,
                target.ImagePath,
                target.RelatedRentalPostId,
                report.Reason,
                report.Description,
                report.Status,
                report.ResolutionNote,
                report.CreatedAt,
                report.UpdatedAt);
        }).ToArray();
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

    private static Guid[] GetTargetIds(IReadOnlyList<Report> reports, ReportTargetType targetType) =>
        reports
            .Where(report => report.TargetType == targetType)
            .Select(report => report.TargetId)
            .Distinct()
            .ToArray();

    private static ReportTargetDisplay ResolveReportTarget(
        Report report,
        Dictionary<Guid, UserProfile> profiles,
        Dictionary<Guid, RentalPost> rentalPosts,
        Dictionary<Guid, MarketplacePost> marketplacePosts,
        Dictionary<Guid, RoommateInvitation> invitations,
        Dictionary<Guid, RentalReview> reviews,
        Dictionary<Guid, RentalWantedPost> wantedPosts)
    {
        switch (report.TargetType)
        {
            case ReportTargetType.RentalPost when rentalPosts.TryGetValue(report.TargetId, out var rentalPost):
                return new ReportTargetDisplay(rentalPost.Title, rentalPost.Media.OrderBy(media => media.SortOrder).FirstOrDefault()?.Path, rentalPost.Id);
            case ReportTargetType.User when profiles.TryGetValue(report.TargetId, out var user):
                return new ReportTargetDisplay(user.DisplayName, user.AvatarPath, null);
            case ReportTargetType.MarketplacePost when marketplacePosts.TryGetValue(report.TargetId, out var marketplacePost):
                return new ReportTargetDisplay(marketplacePost.Title, marketplacePost.Media.OrderBy(media => media.SortOrder).FirstOrDefault()?.Url, marketplacePost.LinkedRentalPostId);
            case ReportTargetType.RoommateInvitation when invitations.TryGetValue(report.TargetId, out var invitation):
                return rentalPosts.TryGetValue(invitation.RentalPostId, out var invitationPost)
                    ? new ReportTargetDisplay($"Lời mời ở ghép cho “{invitationPost.Title}”", invitationPost.Id)
                    : new ReportTargetDisplay("Lời mời ở ghép", invitation.RentalPostId);
            case ReportTargetType.RentalReview when reviews.TryGetValue(report.TargetId, out var review):
                return rentalPosts.TryGetValue(review.RentalPostId, out var reviewPost)
                    ? new ReportTargetDisplay($"Đánh giá {review.Rating} sao cho “{reviewPost.Title}”", reviewPost.Id)
                    : new ReportTargetDisplay($"Đánh giá {review.Rating} sao", review.RentalPostId);
            case ReportTargetType.RentalWantedPost when wantedPosts.TryGetValue(report.TargetId, out var wantedPost):
                return new ReportTargetDisplay(wantedPost.Title, null);
            default:
                return new ReportTargetDisplay("Nội dung không còn tồn tại", null);
        }
    }

    private sealed record ReportTargetDisplay(string DisplayName, string? ImagePath, Guid? RelatedRentalPostId)
    {
        public ReportTargetDisplay(string displayName, Guid? relatedRentalPostId)
            : this(displayName, null, relatedRentalPostId)
        {
        }
    }
}
